using System.Data;
using Microsoft.Data.SqlClient;
using PhishGuard.Core.Domain;

namespace PhishGuard.Data;

/// <summary>SqlBulkCopy loader: 500K URLs in seconds instead of minutes through EF change tracking.</summary>
public sealed class BulkLoader(string connectionString)
{
    public async Task LoadUrlsAsync(IEnumerable<UrlRecord> urls, int batchSize = 50_000, CancellationToken ct = default)
    {
        var table = new DataTable();
        table.Columns.Add(nameof(UrlRecord.Url), typeof(string));
        table.Columns.Add(nameof(UrlRecord.Host), typeof(string));
        table.Columns.Add(nameof(UrlRecord.RegisteredDomain), typeof(string));
        table.Columns.Add(nameof(UrlRecord.Tld), typeof(string));
        table.Columns.Add(nameof(UrlRecord.Source), typeof(string));
        table.Columns.Add(nameof(UrlRecord.Category), typeof(string));
        table.Columns.Add(nameof(UrlRecord.IsPhishing), typeof(bool));
        table.Columns.Add(nameof(UrlRecord.TargetBrandId), typeof(int));
        table.Columns.Add(nameof(UrlRecord.IngestedAtUtc), typeof(DateTime));

        foreach (var u in urls)
        {
            table.Rows.Add(Truncate(u.Url, 2048), Truncate(u.Host, 253), Truncate(u.RegisteredDomain, 253), Truncate(u.Tld, 63),
                u.Source.ToString(), u.Category.ToString(), u.IsPhishing, (object?)u.TargetBrandId ?? DBNull.Value, u.IngestedAtUtc);
            if (table.Rows.Count >= batchSize) { await WriteAsync("Urls", table, ct); table.Clear(); }
        }
        if (table.Rows.Count > 0) await WriteAsync("Urls", table, ct);
    }

    public async Task LoadEvidenceAsync(IEnumerable<DomainEvidence> evidence, CancellationToken ct = default)
    {
        var table = new DataTable();
        string[] cols =
        [
            nameof(DomainEvidence.RegisteredDomain), nameof(DomainEvidence.CreatedOnUtc), nameof(DomainEvidence.Registrar),
            nameof(DomainEvidence.RegistrarAbuseEmail), nameof(DomainEvidence.RegistrantCountry), nameof(DomainEvidence.PrivacyProtected),
            nameof(DomainEvidence.HostingProvider), nameof(DomainEvidence.HostingAbuseEmail), nameof(DomainEvidence.CertIssuer),
            nameof(DomainEvidence.CertIssuedOnUtc), nameof(DomainEvidence.PageTitle), nameof(DomainEvidence.HasLoginForm),
            nameof(DomainEvidence.BrandLogoDetected), nameof(DomainEvidence.ScreenshotRef),
            nameof(DomainEvidence.SandboxRunAtUtc), nameof(DomainEvidence.SandboxAcceptsAnyCredentials),
            nameof(DomainEvidence.SandboxPostsToExternalHost), nameof(DomainEvidence.SandboxPostTarget),
            nameof(DomainEvidence.SandboxRedirectsToBrand), nameof(DomainEvidence.BrandPageSimilarity)
        ];
        Type[] types =
        [
            typeof(string), typeof(DateTime), typeof(string), typeof(string), typeof(string), typeof(bool), typeof(string),
            typeof(string), typeof(string), typeof(DateTime), typeof(string), typeof(bool), typeof(bool), typeof(string),
            typeof(DateTime), typeof(bool), typeof(bool), typeof(string), typeof(bool), typeof(float)
        ];
        for (var i = 0; i < cols.Length; i++) table.Columns.Add(cols[i], types[i]);

        foreach (var e in evidence)
            table.Rows.Add(e.RegisteredDomain, N(e.CreatedOnUtc), N(e.Registrar), N(e.RegistrarAbuseEmail), N(e.RegistrantCountry),
                e.PrivacyProtected, N(e.HostingProvider), N(e.HostingAbuseEmail), N(e.CertIssuer), N(e.CertIssuedOnUtc),
                N(e.PageTitle), e.HasLoginForm, e.BrandLogoDetected, N(e.ScreenshotRef),
                N(e.SandboxRunAtUtc), N(e.SandboxAcceptsAnyCredentials), N(e.SandboxPostsToExternalHost), N(e.SandboxPostTarget),
                N(e.SandboxRedirectsToBrand), N(e.BrandPageSimilarity));

        await WriteAsync("DomainEvidence", table, ct);
    }

    public async Task LoadShortLinksAsync(IEnumerable<ShortLink> links, CancellationToken ct = default)
    {
        var table = new DataTable();
        table.Columns.Add(nameof(ShortLink.ShortUrl), typeof(string));
        table.Columns.Add(nameof(ShortLink.ExpandedUrl), typeof(string));
        table.Columns.Add(nameof(ShortLink.ExpandedAtUtc), typeof(DateTime));
        foreach (var l in links) table.Rows.Add(Truncate(l.ShortUrl, 400), Truncate(l.ExpandedUrl, 2048), l.ExpandedAtUtc);
        if (table.Rows.Count > 0) await WriteAsync("ShortLinks", table, ct);
    }

    public async Task LoadScoresAsync(IEnumerable<UrlScore> scores, CancellationToken ct = default)
    {
        var table = new DataTable();
        table.Columns.Add(nameof(UrlScore.UrlId), typeof(long));
        table.Columns.Add(nameof(UrlScore.ModelVersion), typeof(string));
        table.Columns.Add(nameof(UrlScore.Probability), typeof(float));
        table.Columns.Add(nameof(UrlScore.Band), typeof(string));
        table.Columns.Add(nameof(UrlScore.ScoredAtUtc), typeof(DateTime));
        foreach (var s in scores)
            table.Rows.Add(s.UrlId, s.ModelVersion, s.Probability, s.Band.ToString(), s.ScoredAtUtc);
        await WriteAsync("UrlScores", table, ct);
    }

    /// <summary>Sets UrlScores.CampaignId for many rows in one round trip (temp table + UPDATE ... JOIN).</summary>
    public async Task AssignCampaignsAsync(IEnumerable<(long ScoreId, long CampaignId)> assignments, CancellationToken ct = default)
    {
        var table = new DataTable();
        table.Columns.Add("ScoreId", typeof(long));
        table.Columns.Add("CampaignId", typeof(long));
        foreach (var (s, c) in assignments) table.Rows.Add(s, c);
        if (table.Rows.Count == 0) return;

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using (var create = new SqlCommand("CREATE TABLE #CampaignMap (ScoreId bigint PRIMARY KEY, CampaignId bigint NOT NULL);", conn))
            await create.ExecuteNonQueryAsync(ct);
        using (var bulk = new SqlBulkCopy(conn) { DestinationTableName = "#CampaignMap", BulkCopyTimeout = 600 })
            await bulk.WriteToServerAsync(table, ct);
        await using var update = new SqlCommand(
            "UPDATE s SET s.CampaignId = m.CampaignId FROM UrlScores s JOIN #CampaignMap m ON m.ScoreId = s.Id;", conn) { CommandTimeout = 600 };
        await update.ExecuteNonQueryAsync(ct);
    }

    private async Task WriteAsync(string tableName, DataTable table, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, null)
        {
            DestinationTableName = tableName,
            BatchSize = table.Rows.Count,
            BulkCopyTimeout = 600
        };
        foreach (DataColumn c in table.Columns) bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
        await bulk.WriteToServerAsync(table, ct);
    }

    private static object N(object? v) => v ?? DBNull.Value;
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
