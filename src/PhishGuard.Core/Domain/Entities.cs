namespace PhishGuard.Core.Domain;

/// <summary>A brand the team protects. Keywords are the tokens attackers reuse (paypal, pp, ...).</summary>
public class ProtectedBrand
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string PrimaryDomain { get; set; }
    /// <summary>Comma-separated tokens, e.g. "paypal,pay-pal".</summary>
    public required string Keywords { get; set; }
    /// <summary>Comma-separated domains the brand legitimately owns (never phishing).</summary>
    public string OwnedDomains { get; set; } = "";
}

/// <summary>One incoming URL plus its ground-truth label (synthetic or from a feed).</summary>
public class UrlRecord
{
    public long Id { get; set; }
    public required string Url { get; set; }
    public required string Host { get; set; }
    public required string RegisteredDomain { get; set; }
    public required string Tld { get; set; }
    public UrlSource Source { get; set; }
    public UrlCategory Category { get; set; }
    /// <summary>Ground truth. Used for training/evaluation only, never shown to the scorer.</summary>
    public bool IsPhishing { get; set; }
    public int? TargetBrandId { get; set; }
    public ProtectedBrand? TargetBrand { get; set; }
    public DateTime IngestedAtUtc { get; set; }
}

/// <summary>
/// Simulated enrichment for a registered domain (WHOIS/RDAP, certificate, page snapshot).
/// Agents read this instead of touching the live site.
/// </summary>
public class DomainEvidence
{
    public required string RegisteredDomain { get; set; }
    public DateTime? CreatedOnUtc { get; set; }
    public string? Registrar { get; set; }
    public string? RegistrarAbuseEmail { get; set; }
    public string? RegistrantCountry { get; set; }
    public bool PrivacyProtected { get; set; }
    public string? HostingProvider { get; set; }
    public string? HostingAbuseEmail { get; set; }
    public string? CertIssuer { get; set; }
    public DateTime? CertIssuedOnUtc { get; set; }
    public string? PageTitle { get; set; }
    public bool HasLoginForm { get; set; }
    public bool BrandLogoDetected { get; set; }
    public string? ScreenshotRef { get; set; }

    // ---- Sandbox detonation (analyst step 4: dummy credentials), simulated ----
    // A real deployment runs this in an isolated browser sandbox, never on an analyst's machine.
    // Null = the sandbox has not run for this domain.
    public DateTime? SandboxRunAtUtc { get; set; }
    /// <summary>The login form accepted made-up credentials (a real login would reject them).</summary>
    public bool? SandboxAcceptsAnyCredentials { get; set; }
    /// <summary>The form posted the dummy data to a different host than the page's own.</summary>
    public bool? SandboxPostsToExternalHost { get; set; }
    public string? SandboxPostTarget { get; set; }
    /// <summary>After submitting, the victim was silently forwarded to the genuine brand site (hides the theft).</summary>
    public bool? SandboxRedirectsToBrand { get; set; }
    /// <summary>Visual similarity (0–1) of the page to the impersonated brand's real login page.</summary>
    public float? BrandPageSimilarity { get; set; }
}

/// <summary>A blocked indicator (domain, URL or IP) added when a case is confirmed. Exported for block-lists.</summary>
public class Indicator
{
    public long Id { get; set; }
    public IndicatorType Type { get; set; }
    public required string Value { get; set; }
    public long CaseId { get; set; }
    public required string Source { get; set; }
    public DateTime AddedAtUtc { get; set; }
}

/// <summary>
/// Where a short link really points, as a shortener's expand API would report it (e.g. bit.ly /v4/expand).
/// Lets the model judge the destination instead of "bit.ly/abc123". Simulated by DataGen; nobody visits the target.
/// </summary>
public class ShortLink
{
    public required string ShortUrl { get; set; }
    public required string ExpandedUrl { get; set; }
    public DateTime ExpandedAtUtc { get; set; }
}

public class UrlScore
{
    public long Id { get; set; }
    public long UrlId { get; set; }
    public UrlRecord? Url { get; set; }
    public required string ModelVersion { get; set; }
    public float Probability { get; set; }
    public ScoreBand Band { get; set; }
    public long? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public DateTime ScoredAtUtc { get; set; }
}

/// <summary>A group of flagged URLs analysts can decide on together (same domain / kit / brand).</summary>
public class Campaign
{
    public long Id { get; set; }
    public required string GroupingKey { get; set; }
    public required string RegisteredDomain { get; set; }
    public string? PathPattern { get; set; }
    public int? BrandId { get; set; }
    public ProtectedBrand? Brand { get; set; }
    public int UrlCount { get; set; }
    public float MaxProbability { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<UrlScore> Scores { get; set; } = [];
}

public class PhishCase
{
    public long Id { get; set; }
    public long CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public CaseStatus Status { get; set; }
    public Verdict Verdict { get; set; }
    public float? Confidence { get; set; }
    public string? Rationale { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<AgentRun> AgentRuns { get; set; } = [];
    public List<AnalystDecision> Decisions { get; set; } = [];
    public List<EnforcementNotice> Notices { get; set; } = [];
    public List<MonitorCheck> MonitorChecks { get; set; } = [];
}

public class AgentRun
{
    public long Id { get; set; }
    public long CaseId { get; set; }
    public required string AgentName { get; set; }
    public string? Input { get; set; }
    public string? Output { get; set; }
    public int? TokensUsed { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

/// <summary>Analyst feedback; becomes training labels for the next model version.</summary>
public class AnalystDecision
{
    public long Id { get; set; }
    public long CaseId { get; set; }
    public required string Analyst { get; set; }
    public Verdict Decision { get; set; }
    public string? Notes { get; set; }
    public DateTime DecidedAtUtc { get; set; }
}

/// <summary>Simulated takedown notice. Nothing is ever sent externally.</summary>
public class EnforcementNotice
{
    public long Id { get; set; }
    public long CaseId { get; set; }
    public EnforcementRoute Route { get; set; }
    public required string Recipient { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public NoticeStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? FiledAtUtc { get; set; }
}

public class MonitorCheck
{
    public long Id { get; set; }
    public long CaseId { get; set; }
    public DateTime CheckedAtUtc { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}
