using System.Text;
using Microsoft.EntityFrameworkCore;
using PhishGuard.Core.Domain;
using PhishGuard.Data;

namespace PhishGuard.Api;

/// <summary>Read-only endpoints over what the Scoring worker produced (UrlScores, Campaigns, Cases).</summary>
public static class ResultsEndpoints
{
    public static RouteGroupBuilder MapResultsEndpoints(this RouteGroupBuilder api)
    {
        // Funnel numbers for the dashboard. The "evaluation" block uses the dataset's ground-truth labels,
        // which only exist because the data is synthetic — it is for measuring the model, not for detection.
        api.MapGet("/scoring/summary", async (PhishGuardDbContext db) =>
        {
            var version = await LatestVersionAsync(db);
            if (version is null) return Results.Ok(new { modelVersion = (string?)null, message = "Nothing scored yet. Run PhishGuard.Training, then the Scoring worker." });

            var byBand = await db.Scores.Where(s => s.ModelVersion == version)
                .GroupBy(s => new { s.Band, s.Url!.IsPhishing })
                .Select(g => new { g.Key.Band, g.Key.IsPhishing, Count = g.Count() })
                .ToListAsync();
            var cases = await db.Cases.Where(c => c.Campaign!.GroupingKey.StartsWith(version + "|"))
                .GroupBy(c => c.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
            var campaigns = await db.Campaigns.CountAsync(c => c.GroupingKey.StartsWith(version + "|"));

            return Results.Ok(new
            {
                modelVersion = version,
                totalScored = byBand.Sum(b => b.Count),
                bands = Enum.GetValues<ScoreBand>().ToDictionary(b => b.ToString(), b => byBand.Where(x => x.Band == b).Sum(x => x.Count)),
                campaigns,
                casesByStatus = cases.ToDictionary(c => c.Status.ToString(), c => c.Count),
                evaluation = Enum.GetValues<ScoreBand>().ToDictionary(b => b.ToString(), b => new
                {
                    phishing = byBand.Where(x => x.Band == b && x.IsPhishing).Sum(x => x.Count),
                    benign = byBand.Where(x => x.Band == b && !x.IsPhishing).Sum(x => x.Count)
                })
            });
        });

        // Detected phishing URLs. Default: auto-confirmed only; includeGray=true adds the uncertain band.
        // format=csv returns a downloadable file (e.g. for a block-list or the takedown team).
        api.MapGet("/phishing", async (PhishGuardDbContext db, bool includeGray = false, int? brandId = null,
            DateTime? since = null, int page = 1, int pageSize = 100, string format = "json") =>
        {
            var version = await LatestVersionAsync(db);
            if (version is null) return Results.Ok(Array.Empty<object>());

            var q = db.Scores.AsNoTracking().Where(s => s.ModelVersion == version &&
                (s.Band == ScoreBand.Confirmed || (includeGray && s.Band == ScoreBand.Gray)));
            if (brandId is not null) q = q.Where(s => s.Campaign!.BrandId == brandId);
            if (since is not null) q = q.Where(s => s.ScoredAtUtc >= since);

            var csv = format.Equals("csv", StringComparison.OrdinalIgnoreCase);
            pageSize = csv ? Math.Clamp(pageSize, 1, 500_000) : Math.Clamp(pageSize, 1, 1000);
            var rows = await q.OrderByDescending(s => s.Probability).ThenBy(s => s.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(s => new PhishingRow(s.UrlId, s.Url!.Url, s.Url.RegisteredDomain, s.Probability, s.Band.ToString(),
                    s.CampaignId, s.Campaign!.Brand!.Name, s.ScoredAtUtc))
                .ToListAsync();

            if (!csv) return Results.Ok(rows);
            var sb = new StringBuilder("url_id,url,registered_domain,probability,band,campaign_id,brand,scored_at_utc\n");
            foreach (var r in rows)
                sb.Append(r.UrlId).Append(",\"").Append(r.Url.Replace("\"", "\"\"")).Append("\",").Append(r.RegisteredDomain).Append(',')
                  .Append(r.Probability.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(r.Band).Append(',')
                  .Append(r.CampaignId).Append(',').Append(r.Brand).Append(',').Append(r.ScoredAtUtc.ToString("O")).Append('\n');
            return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"phishing-{version}.csv");
        });

        // Campaign queue, biggest first. status=AgentReview gives the Gray queue, Confirmed the takedown queue.
        api.MapGet("/campaigns", async (PhishGuardDbContext db, CaseStatus? status, int? brandId, int page = 1, int pageSize = 50) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 500);
            var q = db.Cases.AsNoTracking().AsQueryable();
            if (status is not null) q = q.Where(c => c.Status == status);
            if (brandId is not null) q = q.Where(c => c.Campaign!.BrandId == brandId);
            return await q.OrderByDescending(c => c.Campaign!.UrlCount).ThenByDescending(c => c.Campaign!.MaxProbability)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new
                {
                    campaignId = c.CampaignId,
                    caseId = c.Id,
                    status = c.Status.ToString(),
                    verdict = c.Verdict.ToString(),
                    c.Campaign!.RegisteredDomain,
                    c.Campaign.PathPattern,
                    brand = c.Campaign.Brand!.Name,
                    c.Campaign.UrlCount,
                    c.Campaign.MaxProbability,
                    c.CreatedAtUtc
                })
                .ToListAsync();
        });

        // Everything an analyst (or agent) needs for one campaign: case, evidence and the URLs in it.
        api.MapGet("/campaigns/{id:long}", async (PhishGuardDbContext db, long id) =>
        {
            var campaign = await db.Campaigns.AsNoTracking().Include(c => c.Brand).FirstOrDefaultAsync(c => c.Id == id);
            if (campaign is null) return Results.NotFound();
            var @case = await db.Cases.AsNoTracking().FirstOrDefaultAsync(c => c.CampaignId == id);
            var evidence = await db.DomainEvidence.AsNoTracking().FirstOrDefaultAsync(e => e.RegisteredDomain == campaign.RegisteredDomain);
            var urls = await db.Scores.AsNoTracking().Where(s => s.CampaignId == id).OrderByDescending(s => s.Probability).Take(200)
                .Select(s => new { s.UrlId, s.Url!.Url, s.Probability, band = s.Band.ToString(), s.Url.IngestedAtUtc })
                .ToListAsync();
            return Results.Ok(new
            {
                campaign.Id, campaign.RegisteredDomain, campaign.PathPattern, brand = campaign.Brand?.Name,
                campaign.UrlCount, campaign.MaxProbability, campaign.CreatedAtUtc,
                @case = @case is null ? null : new { @case.Id, status = @case.Status.ToString(), verdict = @case.Verdict.ToString(), @case.Confidence, @case.Rationale },
                evidence,
                urls
            });
        });

        return api;
    }

    private static Task<string?> LatestVersionAsync(PhishGuardDbContext db) =>
        db.Scores.OrderByDescending(s => s.Id).Select(s => s.ModelVersion).FirstOrDefaultAsync();

    public record PhishingRow(long UrlId, string Url, string RegisteredDomain, float Probability, string Band, long? CampaignId, string? Brand, DateTime ScoredAtUtc);
}
