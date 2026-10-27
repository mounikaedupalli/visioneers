using System.IO.Compression;

namespace PhishGuard.DataGen;

/// <summary>
/// Public threat feeds and the Tranco benign list. Feeds are only ever read as text: the URLs inside
/// are never requested. Download once with --download-feeds, then keep the files under data/feeds so
/// the team works from a fixed, supplied dataset (hackathon ground rule).
/// </summary>
public static class FeedImporter
{
    public static readonly (string FileName, string Url)[] Feeds =
    [
        ("openphish.txt", "https://openphish.com/feed.txt"),
        ("phishstats.csv", "https://phishstats.info/phish_score.csv"),
    ];

    public const string TrancoUrl = "https://tranco-list.eu/top-1m.csv.zip";

    public static async Task DownloadAsync(string feedsDir, bool includeTranco, CancellationToken ct)
    {
        Directory.CreateDirectory(feedsDir);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PhishGuard-Hackathon-DataGen/1.0");

        foreach (var (file, url) in Feeds)
        {
            try
            {
                var text = await http.GetStringAsync(url, ct);
                await File.WriteAllTextAsync(Path.Combine(feedsDir, file), text, ct);
                Console.WriteLine($"  downloaded {file} ({text.Length / 1024} KB)");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Console.WriteLine($"  skipped {file}: {ex.Message}");
            }
        }

        if (!includeTranco) return;
        try
        {
            await using var zip = await http.GetStreamAsync(TrancoUrl, ct);
            using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
            var entry = archive.Entries.First(e => e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
            entry.ExtractToFile(Path.Combine(feedsDir, "tranco-top-1m.csv"), overwrite: true);
            Console.WriteLine("  downloaded tranco-top-1m.csv");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            Console.WriteLine($"  skipped Tranco: {ex.Message}");
        }
    }

    /// <summary>Reads every *.txt / *.csv feed file in the folder (except Tranco) and returns URL strings.</summary>
    public static IEnumerable<(string Url, string SourceFile)> ReadPhishingFeeds(string feedsDir)
    {
        if (!Directory.Exists(feedsDir)) yield break;
        foreach (var file in Directory.EnumerateFiles(feedsDir).Where(f => f.EndsWith(".txt") || f.EndsWith(".csv")))
        {
            if (Path.GetFileName(file).StartsWith("tranco", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var line in File.ReadLines(file))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                // Plain lists have one URL per line; CSV feeds have the URL in one of the quoted columns.
                var url = line.Split(',').Select(c => c.Trim().Trim('"')).FirstOrDefault(c => c.StartsWith("http", StringComparison.OrdinalIgnoreCase));
                if (url is not null) yield return (url, Path.GetFileName(file));
            }
        }
    }

    /// <summary>Tranco CSV is "rank,domain".</summary>
    public static IEnumerable<string> ReadTranco(string feedsDir, int top)
    {
        var path = Path.Combine(feedsDir, "tranco-top-1m.csv");
        if (!File.Exists(path)) return [];
        return File.ReadLines(path).Select(l => l.Split(',')).Where(p => p.Length == 2).Select(p => p[1].Trim()).Take(top);
    }
}
