namespace PhishGuard.Web;

// DTOs mirroring PhishGuard.Api responses (enums arrive as strings).

public record BrandCount(string Brand, int Cases);

public record DashboardDto(
    string? ModelVersion, int TotalScored,
    Dictionary<string, int> Bands, Dictionary<string, int> CasesByStatus, Dictionary<string, int> CasesByVerdict,
    Dictionary<string, int> NoticesByStatus, int Investigated, int AnalystDecided, int Indicators, List<BrandCount> TopBrands);

public record CaseRow(long Id, string Status, string Verdict, float? Confidence, string RegisteredDomain, string? PathPattern,
    string? Brand, int UrlCount, float ModelProbability, bool Investigated, DateTime UpdatedAtUtc);

public record PagedResult<T>(int Total, int Page, int PageSize, List<T> Items);

public record CampaignDto(long Id, string RegisteredDomain, string? PathPattern, string? Brand, int UrlCount, float MaxProbability);

public record EvidenceDto(
    string RegisteredDomain, DateTime? CreatedOnUtc, string? Registrar, string? RegistrarAbuseEmail, string? RegistrantCountry,
    bool PrivacyProtected, string? HostingProvider, string? HostingAbuseEmail, string? CertIssuer, DateTime? CertIssuedOnUtc,
    string? PageTitle, bool HasLoginForm, bool BrandLogoDetected, string? ScreenshotRef,
    DateTime? SandboxRunAtUtc, bool? SandboxAcceptsAnyCredentials, bool? SandboxPostsToExternalHost, string? SandboxPostTarget,
    bool? SandboxRedirectsToBrand, float? BrandPageSimilarity);

public record StepDto(int Step, string Name, string Level, double Points, string Summary, List<string> Details,
    string? RecordedBy = null, DateTime? RecordedAtUtc = null);

public record UrlRowDto(long UrlId, string Url, string? ExpandedUrl, float Probability, string Band, DateTime IngestedAtUtc);

public record NoticeDto(long Id, long CaseId, string Route, string Recipient, string Subject, string Body, string Status,
    DateTime CreatedAtUtc, DateTime? FiledAtUtc, string? Domain, string? Brand);

public record DecisionDto(string Analyst, string Decision, string? Notes, DateTime DecidedAtUtc);

public record CaseDetailDto(
    long Id, string Status, string Verdict, float? Confidence, string? Rationale, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    CampaignDto Campaign, EvidenceDto? Evidence, List<StepDto> Steps, List<UrlRowDto> Urls, List<NoticeDto> Notices,
    List<DecisionDto> Decisions, int Indicators);

public record IndicatorDto(long Id, string Type, string Value, long CaseId, string Source, DateTime AddedAtUtc);

public record CategoryCount(string Category, bool IsPhishing, int Count);
public record DatasetStats(int TotalUrls, int PhishingUrls, int DomainsWithEvidence, List<CategoryCount> ByCategory);

public static class Display
{
    /// <summary>hxxps://example[.]com — URLs are never rendered as clickable links in the console.</summary>
    public static string Defang(string? url) =>
        string.IsNullOrEmpty(url) ? "" : url.Replace("http", "hxxp", StringComparison.OrdinalIgnoreCase).Replace(".", "[.]");

    public static string Pct(double? v) => v is null ? "—" : $"{v:P0}";

    public static string StatusLabel(string status) => status switch
    {
        "AgentReview" => "Investigating",
        "AnalystReview" => "Needs analyst",
        "TakedownFiled" => "Takedown filed",
        _ => status
    };

    public static string VerdictLabel(string verdict) => verdict == "NeedsHuman" ? "Needs analyst" : verdict;

    public static string StatusClass(string status) => status switch
    {
        "AnalystReview" => "b-warn",
        "AgentReview" => "b-info",
        "Confirmed" => "b-bad",
        "TakedownFiled" => "b-info",
        "Dismissed" => "b-ok",
        "Closed" => "b-neutral",
        _ => "b-neutral"
    };

    public static string VerdictClass(string verdict) => verdict switch
    {
        "Phishing" => "b-bad",
        "Benign" => "b-ok",
        "NeedsHuman" => "b-warn",
        _ => "b-neutral"
    };

    public static string LevelClass(string level) => level switch
    {
        "Malicious" => "b-bad",
        "Suspicious" => "b-warn",
        "Benign" => "b-ok",
        _ => "b-neutral"
    };

    public static string NoticeClass(string status) => status switch
    {
        "Draft" => "b-warn",
        "Filed" => "b-info",
        "Resolved" => "b-ok",
        "Rejected" => "b-neutral",
        _ => "b-neutral"
    };

    public static string YesNo(bool? v) => v switch { true => "Yes", false => "No", null => "Not tested" };
}
