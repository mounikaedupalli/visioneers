using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using PhishGuard.Core.Domain;
using PhishGuard.Core.Urls;
using PhishGuard.Data;
using PhishGuard.Ml;

namespace PhishGuard.Scoring;

/// <summary>
/// Scores every URL that the current model hasn't seen yet, then groups flagged URLs into campaigns:
///   Tier 0  known-feed URL → Confirmed; brand's own domain (no embedded redirect) → Clear
///   Tier 1  ML.NET model → calibrated probability → Clear / Gray / Confirmed (thresholds from model.json)
///   Group   Gray + Confirmed URLs → Campaigns (same domain + path shape + brand) → one Case each
///           (Gray → AgentReview for the agents, Confirmed → Confirmed, ready for takedown)
/// Runs once at start-up and then every minute for newly ingested URLs. Picks up a retrained model automatically.
/// </summary>
public class Worker(ILogger<Worker> logger, IServiceScopeFactory scopes, IConfiguration config) : BackgroundService
{
    private static readonly int EmbeddedUrlIndex = Array.IndexOf(UrlFeatureExtractor.Names, "EmbeddedUrlInQuery");
    private static readonly int BrandOwnedIndex = Array.IndexOf(UrlFeatureExtractor.Names, "BrandOwnedDomain") - UrlFeatureExtractor.LexicalCount;
    private static readonly UrlSource[] FeedSources = [UrlSource.OpenPhish, UrlSource.PhishStats, UrlSource.UrlHaus];

    private readonly MLContext _ml = new(seed: 42);
    private ITransformer? _model;
    private ModelMetadata? _metadata;
    private DateTime _modelWrittenUtc;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var modelDir = config["Scoring:ModelDirectory"] ?? ModelStore.DefaultDirectory();
        var batchSize = config.GetValue("Scoring:BatchSize", 10_000);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (TryLoadModel(modelDir))
                    await ScorePendingAsync(batchSize, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scoring run failed; retrying in a minute");
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private bool TryLoadModel(string modelDir)
    {
        if (!ModelStore.Exists(modelDir))
        {
            logger.LogWarning("No model found in {ModelDir}. Run PhishGuard.Training first.", modelDir);
            return false;
        }
        var written = File.GetLastWriteTimeUtc(Path.Combine(modelDir, ModelStore.MetadataFile));
        if (_metadata is not null && written == _modelWrittenUtc) return true;

        (_model, _metadata) = ModelStore.Load(_ml, modelDir);
        _modelWrittenUtc = written;
        logger.LogInformation("Loaded model {Version}: Clear < {Clear:F4}, Confirmed >= {Confirm:F4}",
            _metadata.Version, _metadata.Bands.ClearThreshold, _metadata.Bands.ConfirmThreshold);
        return true;
    }

    private async Task ScorePendingAsync(int batchSize, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhishGuardDbContext>();
        var loader = new BulkLoader(db.Database.GetConnectionString()!);
        var version = _metadata!.Version;
        var policy = _metadata.Bands;
        var extractor = new UrlFeatureExtractor(await db.Brands.AsNoTracking().ToListAsync(ct));

        var lastId = await db.Scores.Where(s => s.ModelVersion == version).MaxAsync(s => (long?)s.UrlId, ct) ?? 0;
        var sw = Stopwatch.StartNew();
        var scored = 0;

        while (!ct.IsCancellationRequested)
        {
            var batch = await db.Urls.AsNoTracking().Where(u => u.Id > lastId).OrderBy(u => u.Id).Take(batchSize)
                .Select(u => new { u.Id, u.Url, u.Source }).ToListAsync(ct);
            if (batch.Count == 0) break;

            var inputs = new UrlModelInput[batch.Count];
            Parallel.For(0, batch.Count, i => inputs[i] = UrlModelInput.Create(extractor, batch[i].Url));
            var probabilities = _model!.Transform(_ml.Data.LoadFromEnumerable(inputs))
                .GetColumn<float>(nameof(UrlPrediction.Probability)).ToArray();

            var now = DateTime.UtcNow;
            var scores = batch.Select((u, i) => new UrlScore
            {
                UrlId = u.Id,
                ModelVersion = version,
                Probability = probabilities[i],
                Band = FeedSources.Contains(u.Source) ? ScoreBand.Confirmed                                          // Tier 0: known feed
                     : inputs[i].Brand[BrandOwnedIndex] == 1 && inputs[i].Lexical[EmbeddedUrlIndex] == 0 ? ScoreBand.Clear // Tier 0: brand's own site
                     : policy.Band(probabilities[i]),                                                                 // Tier 1: model
                ScoredAtUtc = now
            }).ToList();

            await loader.LoadScoresAsync(scores, ct);
            lastId = batch[^1].Id;
            scored += batch.Count;
            if (scored % (batchSize * 10) == 0) logger.LogInformation("Scored {Count:N0} URLs so far ({Rate:N0}/s)", scored, scored / sw.Elapsed.TotalSeconds);
        }

        if (scored == 0)
        {
            logger.LogInformation("Up to date: no new URLs for model {Version}", version);
            return;
        }

        var (campaigns, cases) = await BuildCampaignsAsync(db, loader, extractor, version, ct);
        var bands = await db.Scores.Where(s => s.ModelVersion == version)
            .GroupBy(s => s.Band).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        logger.LogInformation(
            "Scored {Scored:N0} URLs in {Seconds:F0}s with {Version}: {Clear:N0} Clear | {Gray:N0} Gray | {Confirmed:N0} Confirmed | {Campaigns:N0} new campaigns, {Cases:N0} new cases",
            scored, sw.Elapsed.TotalSeconds, version,
            bands.GetValueOrDefault(ScoreBand.Clear), bands.GetValueOrDefault(ScoreBand.Gray), bands.GetValueOrDefault(ScoreBand.Confirmed),
            campaigns, cases);
    }

    private static async Task<(int Campaigns, int Cases)> BuildCampaignsAsync(
        PhishGuardDbContext db, BulkLoader loader, UrlFeatureExtractor extractor, string version, CancellationToken ct)
    {
        var flagged = await db.Scores.AsNoTracking()
            .Where(s => s.ModelVersion == version && s.Band != ScoreBand.Clear && s.CampaignId == null)
            .Select(s => new { s.Id, s.Band, s.Probability, s.Url!.Url })
            .ToListAsync(ct);
        if (flagged.Count == 0) return (0, 0);

        var groups = flagged.Select(s =>
            {
                var (domain, pattern) = CampaignKey.Parts(s.Url);
                var brandId = extractor.DetectBrand(s.Url);
                return (s.Id, s.Band, s.Probability, domain, pattern, brandId,
                        Key: $"{version}|{CampaignKey.Build(s.Band.ToString(), domain, pattern, brandId)}");
            })
            .GroupBy(x => x.Key)
            .ToList();

        var keys = groups.Select(g => g.Key).ToList();
        var existing = await db.Campaigns.Where(c => keys.Contains(c.GroupingKey)).ToDictionaryAsync(c => c.GroupingKey, ct);
        var now = DateTime.UtcNow;
        var created = new List<Campaign>();

        foreach (var g in groups)
        {
            if (existing.TryGetValue(g.Key, out var campaign))
            {
                campaign.UrlCount += g.Count();
                campaign.MaxProbability = Math.Max(campaign.MaxProbability, g.Max(x => x.Probability));
                continue;
            }
            var first = g.First();
            campaign = new Campaign
            {
                GroupingKey = g.Key,
                RegisteredDomain = first.domain,
                PathPattern = first.pattern,
                BrandId = first.brandId,
                UrlCount = g.Count(),
                MaxProbability = g.Max(x => x.Probability),
                CreatedAtUtc = now
            };
            created.Add(campaign);
            existing[g.Key] = campaign;
        }
        db.Campaigns.AddRange(created);
        await db.SaveChangesAsync(ct);

        // One case per new campaign.
        var bandOf = groups.ToDictionary(g => g.Key, g => g.First().Band);
        db.Cases.AddRange(created.Select(c => bandOf[c.GroupingKey] == ScoreBand.Confirmed
            ? new PhishCase
            {
                CampaignId = c.Id, Status = CaseStatus.Confirmed, Verdict = Verdict.Phishing, Confidence = c.MaxProbability,
                Rationale = $"Auto-confirmed by {version}: every URL scored at or above the Confirmed threshold.",
                CreatedAtUtc = now, UpdatedAtUtc = now
            }
            : new PhishCase
            {
                CampaignId = c.Id, Status = CaseStatus.AgentReview, Verdict = Verdict.Unknown, Confidence = c.MaxProbability,
                Rationale = "Model is uncertain (Gray band); queued for agent review.",
                CreatedAtUtc = now, UpdatedAtUtc = now
            }));
        await db.SaveChangesAsync(ct);

        await loader.AssignCampaignsAsync(groups.SelectMany(g => g.Select(x => (x.Id, existing[g.Key].Id))), ct);
        return (created.Count, created.Count);
    }
}
