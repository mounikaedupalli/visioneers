using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhishGuard.Core.Domain;
using PhishGuard.Core.Urls;
using PhishGuard.Data;
using PhishGuard.DataGen;

// Usage:
//   dotnet run --project src/PhishGuard.DataGen -- [--count 500000] [--phish-rate 0.02] [--seed 42] [--days 14]
//        [--reset] [--download-feeds] [--feeds-dir data/feeds] [--feed-share 0.3] [--csv data/urls.csv]
//        [--new-campaign-rate 0.15]   (share of phishing URLs that start a new attacker campaign)
//        [--difficulty normal|hard]   (hard adds look-alike benign cases, lure-free kits and kits reused across domains)

var opts = Options.Parse(args);
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
var connectionString = config.GetConnectionString("phishguard")
    ?? throw new InvalidOperationException("ConnectionStrings:phishguard is not configured.");

var sw = Stopwatch.StartNew();
var asOf = DateTime.UtcNow.Date;
var brands = BrandCatalog.Seed;

// 1. Database
await using (var db = new PhishGuardDbContext(new DbContextOptionsBuilder<PhishGuardDbContext>().UseSqlServer(connectionString).Options))
{
    if (opts.Reset)
    {
        Console.WriteLine("Dropping database...");
        await db.Database.EnsureDeletedAsync();
    }
    Console.WriteLine("Applying migrations...");
    await db.Database.MigrateAsync();
    if (await db.Urls.AnyAsync())
    {
        Console.WriteLine("Urls table already has data. Re-run with --reset to regenerate.");
        return 1;
    }
}

// 2. Optional public feeds (read as text only — never fetched)
if (opts.DownloadFeeds)
{
    Console.WriteLine($"Downloading public feeds into {opts.FeedsDir} ...");
    await FeedImporter.DownloadAsync(opts.FeedsDir, includeTranco: true, CancellationToken.None);
}
var tranco = FeedImporter.ReadTranco(opts.FeedsDir, top: 50_000).ToList();
var feedUrls = FeedImporter.ReadPhishingFeeds(opts.FeedsDir).DistinctBy(f => f.Url).ToList();
Console.WriteLine($"Feeds: {feedUrls.Count:N0} phishing URLs, {tranco.Count:N0} Tranco domains");
Console.WriteLine($"Difficulty: {(opts.Hard ? "hard" : "normal")}");

// 3. Generate
var gen = new SyntheticUrlGenerator(opts.Seed, brands, tranco) { WindowDays = opts.Days, NewCampaignRate = opts.NewCampaignRate, Hard = opts.Hard };
var rng = new Random(opts.Seed + 1);
var phishingTarget = (int)Math.Round(opts.Count * opts.PhishRate);
var benignTarget = opts.Count - phishingTarget;
var feedTarget = Math.Min(feedUrls.Count, (int)(phishingTarget * opts.FeedShare));

var generated = new List<GeneratedUrl>(opts.Count);
for (var i = 0; i < benignTarget; i++)
    generated.Add(gen.Generate(gen.PickCategory(gen.BenignCategories)));
foreach (var (url, _) in feedUrls.OrderBy(_ => rng.Next()).Take(feedTarget))
    generated.Add(new GeneratedUrl(url, UrlCategory.FeedReal, GuessBrand(url, brands), DomainOwner.Attacker));
for (var i = generated.Count - benignTarget; i < phishingTarget; i++)
    generated.Add(gen.GeneratePhishing(gen.PickCategory(gen.PhishingCategories)));

// "Emerging tactics": homoglyph and encoded-long attacks only appear in the last third of the window,
// so a time-based train/test split shows how the model copes with a tactic it hasn't seen.
// Synthetic phishing carries its own campaign time (Day); benign and feed URLs are spread over the window.
var emerging = SyntheticUrlGenerator.EmergingTactics;
var start = asOf.AddDays(-opts.Days);
var records = new List<UrlRecord>(generated.Count);
var observations = new List<(string, DomainOwner, int?, bool)>(generated.Count);
var feedSourceByUrl = feedUrls.ToDictionary(f => f.Url, f => f.SourceFile);
var skipped = 0;

foreach (var g in generated.OrderBy(_ => rng.Next()))
{
    if (!UrlParser.TryParse(g.Url, out var p)) { skipped++; continue; }
    var windowStart = emerging.Contains(g.Category) ? opts.Days * 2.0 / 3 : 0;
    var ingested = start.AddDays(g.Day ?? windowStart + rng.NextDouble() * (opts.Days - windowStart));
    records.Add(new UrlRecord
    {
        Url = g.Url,
        Host = p.Host,
        RegisteredDomain = p.RegisteredDomain,
        Tld = p.Tld,
        Source = g.Category == UrlCategory.FeedReal ? SourceOf(feedSourceByUrl[g.Url]) : UrlSource.Synthetic,
        Category = g.Category,
        IsPhishing = g.Category.IsPhishing(),
        TargetBrandId = g.BrandId,
        IngestedAtUtc = ingested
    });
    observations.Add((p.RegisteredDomain, g.Owner, g.BrandId, p.IsIpHost));
}
records.Sort((a, b) => a.IngestedAtUtc.CompareTo(b.IngestedAtUtc));
Console.WriteLine($"Generated {records.Count:N0} URLs in {sw.Elapsed.TotalSeconds:F1}s ({skipped} unparseable skipped)");
var campaignSizes = generated.Where(g => g.CampaignId is not null).GroupBy(g => g.CampaignId).Select(c => c.Count()).OrderDescending().ToList();
if (campaignSizes.Count > 0)
    Console.WriteLine($"Attacker campaigns: {campaignSizes.Count:N0} (largest {campaignSizes[0]:N0} URLs, median {campaignSizes[campaignSizes.Count / 2]}, " +
                      $"{campaignSizes.Count(s => s == 1):N0} single-URL); phishing registered domains: " +
                      $"{records.Where(r => r.IsPhishing).Select(r => r.RegisteredDomain).Distinct().Count():N0}");

var evidence = new EvidenceGenerator(opts.Seed + 2, brands, asOf).Build(observations).ToList();
Console.WriteLine($"Generated evidence for {evidence.Count:N0} registered domains");

// 4. Load
var loader = new BulkLoader(connectionString);
await loader.LoadUrlsAsync(records);
await loader.LoadEvidenceAsync(evidence);
Console.WriteLine($"Loaded into SQL Server in {sw.Elapsed.TotalSeconds:F1}s total");

if (opts.CsvPath is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(opts.CsvPath))!);
    await using var w = new StreamWriter(opts.CsvPath, false, new UTF8Encoding(false));
    await w.WriteLineAsync("url,category,is_phishing,target_brand_id,ingested_at_utc");
    foreach (var r in records)
        await w.WriteLineAsync($"\"{r.Url.Replace("\"", "\"\"")}\",{r.Category},{(r.IsPhishing ? 1 : 0)},{r.TargetBrandId},{r.IngestedAtUtc:O}");
    Console.WriteLine($"Wrote {opts.CsvPath}");
}

// 5. Summary
Console.WriteLine();
Console.WriteLine($"{"Category",-22}{"Label",-10}{"Count",10}");
foreach (var grp in records.GroupBy(r => r.Category).OrderBy(g => (int)g.Key))
    Console.WriteLine($"{grp.Key,-22}{(grp.Key.IsPhishing() ? "phishing" : "benign"),-10}{grp.Count(),10:N0}");
Console.WriteLine($"{"TOTAL",-22}{"",-10}{records.Count,10:N0}   phishing = {records.Count(r => r.IsPhishing):N0} ({100.0 * records.Count(r => r.IsPhishing) / records.Count:F2}%)");
return 0;

static int? GuessBrand(string url, IReadOnlyList<ProtectedBrand> brands) =>
    brands.FirstOrDefault(b => b.Keywords.Split(',').Any(k => url.Contains(k, StringComparison.OrdinalIgnoreCase)))?.Id;

static UrlSource SourceOf(string file) =>
    file.StartsWith("openphish", StringComparison.OrdinalIgnoreCase) ? UrlSource.OpenPhish
    : file.StartsWith("phishstats", StringComparison.OrdinalIgnoreCase) ? UrlSource.PhishStats
    : file.StartsWith("urlhaus", StringComparison.OrdinalIgnoreCase) ? UrlSource.UrlHaus
    : UrlSource.Manual;

internal sealed record Options(int Count, double PhishRate, int Seed, int Days, bool Reset, bool DownloadFeeds, string FeedsDir, double FeedShare, string? CsvPath, double NewCampaignRate, bool Hard)
{
    public static Options Parse(string[] args)
    {
        string? Get(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        bool Has(string name) => args.Contains(name);
        return new Options(
            Count: int.Parse(Get("--count") ?? "500000"),
            PhishRate: double.Parse(Get("--phish-rate") ?? "0.02", System.Globalization.CultureInfo.InvariantCulture),
            Seed: int.Parse(Get("--seed") ?? "42"),
            Days: int.Parse(Get("--days") ?? "14"),
            Reset: Has("--reset"),
            DownloadFeeds: Has("--download-feeds"),
            FeedsDir: Get("--feeds-dir") ?? Path.Combine("data", "feeds"),
            FeedShare: double.Parse(Get("--feed-share") ?? "0.3", System.Globalization.CultureInfo.InvariantCulture),
            CsvPath: Get("--csv"),
            NewCampaignRate: double.Parse(Get("--new-campaign-rate") ?? "0.15", System.Globalization.CultureInfo.InvariantCulture),
            Hard: (Get("--difficulty") ?? "normal").Equals("hard", StringComparison.OrdinalIgnoreCase));
    }
}
