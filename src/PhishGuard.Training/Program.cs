using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.ML;
using Microsoft.ML.Data;
using PhishGuard.Core.Domain;
using PhishGuard.Core.Urls;
using PhishGuard.Data;
using PhishGuard.Ml;

// Trains the Tier-1 model and compares it with a lexical-only baseline (the sponsor's current approach).
// Usage:
//   dotnet run --project src/PhishGuard.Training -- [--iterations 300] [--target-recall 0.98]
//        [--target-precision 0.995] [--out models] [--daily-volume 1500000] [--max-clear 0.5] [--min-confirm 0.9]

var opts = TrainOptions.Parse(args);
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
var connectionString = config.GetConnectionString("phishguard")
    ?? throw new InvalidOperationException("ConnectionStrings:phishguard is not configured.");
var outDir = opts.OutDir ?? ModelStore.DefaultDirectory();
var sw = Stopwatch.StartNew();

// 1. Load, oldest first. Short links are judged by their destination ("effective URL"), and every row
//    carries the evidence of its effective registered domain.
List<(string Url, bool IsPhishing, UrlCategory Category, string Domain, DateTime SeenAt)> rows;
List<ProtectedBrand> brands;
Dictionary<string, string> expansions;
Dictionary<string, DomainEvidence> evidence;
await using (var db = new PhishGuardDbContext(new DbContextOptionsBuilder<PhishGuardDbContext>().UseSqlServer(connectionString, o => o.CommandTimeout(300)).Options))
{
    brands = await db.Brands.AsNoTracking().ToListAsync();
    expansions = await db.ShortLinks.AsNoTracking().ToDictionaryAsync(s => s.ShortUrl, s => s.ExpandedUrl, StringComparer.Ordinal);
    evidence = await db.DomainEvidence.AsNoTracking().ToDictionaryAsync(e => e.RegisteredDomain, StringComparer.OrdinalIgnoreCase);
    rows = (await db.Urls.AsNoTracking().OrderBy(u => u.IngestedAtUtc).ThenBy(u => u.Id)
            .Select(u => new { u.Url, u.IsPhishing, u.Category, u.RegisteredDomain, u.IngestedAtUtc }).ToListAsync())
        .Select(u =>
        {
            var effective = UrlModelInput.Effective(u.Url, expansions);
            var domain = effective == u.Url || !UrlParser.TryParse(effective, out var p) ? u.RegisteredDomain : p.RegisteredDomain;
            return (Url: effective, u.IsPhishing, u.Category, Domain: domain, SeenAt: u.IngestedAtUtc);
        }).ToList();
}
if (rows.Count == 0) { Console.WriteLine("No URLs in the database. Run PhishGuard.DataGen first."); return 1; }
Console.WriteLine($"Loaded {rows.Count:N0} URLs, {brands.Count} brands, {expansions.Count:N0} short-link expansions, {evidence.Count:N0} evidence records in {sw.Elapsed.TotalSeconds:F1}s");

// 2. Time-based split: fit on the oldest 60%, pick thresholds on the next 10%, test on the newest 30%.
//    "Emerging" tactics (homoglyph, encoded-long) start late, so they are mostly unseen during fitting.
var fitEnd = (int)(rows.Count * 0.60);
var valEnd = (int)(rows.Count * 0.70);

// Attacker campaigns reuse domains for days, so many validation/test phishing URLs share a domain the model was
// fitted on. Those are easy (and in production would already be on the block-list). Thresholds are therefore
// chosen on phishing from domains NOT seen in fitting, plus all benign rows — the realistic hard case.
var fitPhishDomains = rows[..fitEnd].Where(r => r.IsPhishing).Select(r => r.Domain).ToHashSet(StringComparer.OrdinalIgnoreCase);
var seenPhishDomains = rows[..valEnd].Where(r => r.IsPhishing).Select(r => r.Domain).ToHashSet(StringComparer.OrdinalIgnoreCase);
var policyRows = Enumerable.Range(fitEnd, valEnd - fitEnd).Where(i => !rows[i].IsPhishing || !fitPhishDomains.Contains(rows[i].Domain)).ToArray();
if (!policyRows.Any(i => rows[i].IsPhishing)) policyRows = Enumerable.Range(fitEnd, valEnd - fitEnd).ToArray();

// 3. Features
var extractor = new UrlFeatureExtractor(brands);
var inputs = new UrlModelInput[rows.Count];
Parallel.For(0, rows.Count, i => inputs[i] = UrlModelInput.Create(extractor, rows[i].Url, rows[i].IsPhishing,
    evidence.GetValueOrDefault(rows[i].Domain), rows[i].SeenAt));
Console.WriteLine($"Extracted {UrlFeatureExtractor.Count} URL + {EvidenceFeatures.Size} evidence features per URL in {sw.Elapsed.TotalSeconds:F1}s");

var ml = new MLContext(seed: 42);
var fit = ml.Data.LoadFromEnumerable(inputs[..fitEnd]);
var validation = ml.Data.LoadFromEnumerable(inputs[fitEnd..valEnd]);
var test = ml.Data.LoadFromEnumerable(inputs[valEnd..]);
var testRows = rows[valEnd..];

// 4. Train the baseline, PhishGuard on the URL alone, and PhishGuard + domain evidence (the one Scoring uses).
var results = new List<ModelResult>();
foreach (var (name, pipeline) in new[]
{
    ("Baseline (URL structure only)", UrlModelPipelines.Baseline(ml, opts.Iterations)),
    ("PhishGuard URL-only (structure + brand + char n-grams)", UrlModelPipelines.Full(ml, opts.Iterations, useEvidence: false)),
    ("PhishGuard + evidence (domain age, certificate, page snapshot)", UrlModelPipelines.Full(ml, opts.Iterations, useEvidence: true))
})
{
    var t = Stopwatch.StartNew();
    Console.WriteLine($"Training {name} ...");
    var model = pipeline.Fit(fit);
    var valProbs = Probabilities(model.Transform(validation));
    var policy = BandPolicy.Choose(policyRows.Select(i => (valProbs[i - fitEnd], rows[i].IsPhishing)).ToList(),
        opts.TargetRecall, opts.TargetPrecision, opts.MaxClear, opts.MinConfirm);
    var scoredTest = model.Transform(test);
    var testProbs = Probabilities(scoredTest);
    var eval = ml.BinaryClassification.Evaluate(scoredTest, labelColumnName: nameof(UrlModelInput.Label));
    results.Add(new ModelResult(name, model, policy, testProbs, eval.AreaUnderPrecisionRecallCurve, eval.AreaUnderRocCurve));
    Console.WriteLine($"  done in {t.Elapsed.TotalSeconds:F0}s  PR-AUC {eval.AreaUnderPrecisionRecallCurve:F4}  thresholds clear<{policy.ClearThreshold:F4}  confirm>={policy.ConfirmThreshold:F4}");
}

// 5. Report
var report = new StringBuilder();
var scale = opts.DailyVolume / testRows.Count;
var testPhish = testRows.Count(r => r.IsPhishing);
report.AppendLine($"# PhishGuard training report — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
report.AppendLine();
report.AppendLine($"Test set: newest {testRows.Count:N0} URLs ({testPhish:N0} phishing). Daily figures scale the test set to {opts.DailyVolume:N0} URLs/day.");
report.AppendLine($"Thresholds chosen on a separate validation slice (phishing from domains not seen in fitting + all benign) for ≥{opts.TargetRecall:P0} recall (Clear, capped at {opts.MaxClear}) and ≥{opts.TargetPrecision:P1} precision (Confirmed, at least {opts.MinConfirm}).");
var newDomainPhish = testRows.Count(r => r.IsPhishing && !seenPhishDomains.Contains(r.Domain));
report.AppendLine($"\"New domains\" = test phishing whose registered domain never appeared as phishing in fitting or validation ({newDomainPhish:N0} URLs).");
report.AppendLine();
report.AppendLine("| Model | PR-AUC | Recall | Recall on new domains | Flagged (Gray+Confirmed) | False positives in flagged | Auto-confirmed | Sent to review (Gray) | Review / day |");
report.AppendLine("|---|---|---|---|---|---|---|---|---|");
foreach (var r in results)
{
    var bands = testRows.Zip(r.TestProbabilities, (row, p) => (row, band: r.Policy.Band(p))).ToList();
    var flagged = bands.Where(b => b.band != ScoreBand.Clear).ToList();
    var caught = flagged.Count(b => b.row.IsPhishing);
    var fp = flagged.Count - caught;
    var gray = bands.Count(b => b.band == ScoreBand.Gray);
    var confirmed = bands.Count(b => b.band == ScoreBand.Confirmed);
    var confirmedPrecision = confirmed == 0 ? 0 : (double)bands.Count(b => b.band == ScoreBand.Confirmed && b.row.IsPhishing) / confirmed;
    var newCaught = flagged.Count(b => b.row.IsPhishing && !seenPhishDomains.Contains(b.row.Domain));
    r.Metrics["recall_new_domains"] = newDomainPhish == 0 ? 0 : (double)newCaught / newDomainPhish;
    report.AppendLine($"| {r.Name} | {r.PrAuc:F4} | {(double)caught / testPhish:P1} | {r.Metrics["recall_new_domains"]:P1} | {flagged.Count:N0} | {fp:N0} ({(flagged.Count == 0 ? 0 : (double)fp / flagged.Count):P1}) | {confirmed:N0} (precision {confirmedPrecision:P1}) | {gray:N0} | {gray * scale:N0} |");
    r.Metrics["pr_auc"] = r.PrAuc;
    r.Metrics["roc_auc"] = r.RocAuc;
    r.Metrics["recall"] = (double)caught / testPhish;
    r.Metrics["flagged_fp_rate"] = flagged.Count == 0 ? 0 : (double)fp / flagged.Count;
    r.Metrics["gray_per_day"] = gray * scale;
    r.Metrics["confirmed_precision"] = confirmedPrecision;
}

report.AppendLine();
report.AppendLine("## Same recall, fewer false alarms");
report.AppendLine("Flagged URLs and false positives at a fixed recall on the test set (single threshold, as the sponsor runs today):");
report.AppendLine();
report.AppendLine("| Model | Recall 90% | Recall 95% | Recall 98% |");
report.AppendLine("|---|---|---|---|");
foreach (var r in results)
{
    var cells = new[] { 0.90, 0.95, 0.98 }.Select(target =>
    {
        var (flagged, fp) = AtRecall(testRows.Select(x => x.IsPhishing).ToArray(), r.TestProbabilities, target);
        return $"{flagged:N0} flagged, {(double)fp / Math.Max(1, flagged):P0} FP";
    });
    report.AppendLine($"| {r.Name} | {string.Join(" | ", cells)} |");
}

var best = results[^1];
report.AppendLine();
report.AppendLine("## Per category");
report.AppendLine("Phishing rows: share caught (not Clear). Benign rows: share wrongly flagged. One column per model, same order as above.");
report.AppendLine();
report.AppendLine($"| Category | Rows in test | {string.Join(" | ", results.Select(r => r.Name.Split(" (")[0]))} | Auto-confirmed ({best.Name.Split(" (")[0]}) |");
report.AppendLine($"|---|---|{string.Concat(results.Select(_ => "---|"))}---|");
var categories = testRows.Select((row, i) => (row.Category, Index: i)).GroupBy(x => x.Category).OrderBy(g => (int)g.Key);
foreach (var g in categories)
{
    var idx = g.Select(x => x.Index).ToArray();
    var cells = results.Select(r => (double)idx.Count(i => r.Policy.Band(r.TestProbabilities[i]) != ScoreBand.Clear) / idx.Length);
    var confirmed = idx.Count(i => best.Policy.Band(best.TestProbabilities[i]) == ScoreBand.Confirmed);
    report.AppendLine($"| {g.Key}{(g.Key is UrlCategory.Homoglyph or UrlCategory.EncodedLongUrl ? " (emerging)" : "")} | {idx.Length:N0} | {string.Join(" | ", cells.Select(c => c.ToString("P1")))} | {confirmed:N0} |");
}

// 6. Save the PhishGuard model for the Scoring worker
var version = $"lgbm-{DateTime.UtcNow:yyyyMMdd-HHmm}";
ModelStore.Save(ml, best.Model, fit.Schema, new ModelMetadata(version, DateTime.UtcNow, best.Policy, fitEnd, testRows.Count, best.Metrics,
    FeatureCount: UrlFeatureExtractor.Count, UsesEvidence: true), outDir);
await File.WriteAllTextAsync(Path.Combine(outDir, "training-report.md"), report.ToString());

Console.WriteLine();
Console.WriteLine(report.ToString());
Console.WriteLine($"Saved {version} to {outDir} ({sw.Elapsed.TotalMinutes:F1} min total)");
return 0;

static float[] Probabilities(IDataView scored) =>
    scored.GetColumn<float>(nameof(UrlPrediction.Probability)).ToArray();

static (int Flagged, int FalsePositives) AtRecall(bool[] labels, float[] probs, double targetRecall)
{
    var phish = probs.Where((_, i) => labels[i]).OrderByDescending(p => p).ToArray();
    var threshold = phish[Math.Max(0, (int)Math.Ceiling(targetRecall * phish.Length) - 1)];
    int flagged = 0, fp = 0;
    for (var i = 0; i < probs.Length; i++)
        if (probs[i] >= threshold) { flagged++; if (!labels[i]) fp++; }
    return (flagged, fp);
}

internal sealed record ModelResult(string Name, ITransformer Model, BandPolicy Policy, float[] TestProbabilities, double PrAuc, double RocAuc)
{
    public Dictionary<string, double> Metrics { get; } = new();
}

internal sealed record TrainOptions(int Iterations, double TargetRecall, double TargetPrecision, string? OutDir, double DailyVolume, float MaxClear, float MinConfirm)
{
    public static TrainOptions Parse(string[] args)
    {
        string? Get(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        double D(string name, double fallback) => Get(name) is { } v ? double.Parse(v, CultureInfo.InvariantCulture) : fallback;
        return new TrainOptions(
            Iterations: int.Parse(Get("--iterations") ?? "300"),
            TargetRecall: D("--target-recall", 0.98),
            TargetPrecision: D("--target-precision", 0.995),
            OutDir: Get("--out"),
            DailyVolume: D("--daily-volume", 1_500_000),
            MaxClear: (float)D("--max-clear", 0.5),
            MinConfirm: (float)D("--min-confirm", 0.9));
    }
}
