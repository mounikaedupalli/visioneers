using System.Net.Http.Json;

namespace PhishGuard.Web;

public sealed class PhishGuardApiClient(HttpClient http)
{
    public Task<DatasetStats?> GetStatsAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<DatasetStats>("/api/stats", ct);
}

public record CategoryCount(string Category, bool IsPhishing, int Count);
public record DatasetStats(int TotalUrls, int PhishingUrls, int DomainsWithEvidence, List<CategoryCount> ByCategory);
