using PhishGuard.Core.Domain;
using PhishGuard.Core.Urls;

namespace PhishGuard.Agents.Investigation;

/// <summary>Result of one step of the analysts' standard operating procedure, automated or recorded by an analyst.</summary>
/// <param name="Points">Contribution to the verdict: positive = more likely phishing, negative = more likely benign.</param>
/// <param name="RecordedBy">Analyst who assessed this step by hand; null for automated findings.</param>
public sealed record StepFinding(int Step, string Name, FindingLevel Level, double Points, string Summary, IReadOnlyList<string> Details,
    string? RecordedBy = null, DateTime? RecordedAtUtc = null)
{
    /// <summary>Verdict points for an analyst's assessment of a step.</summary>
    public static double PointsFor(FindingLevel level) => level switch
    {
        FindingLevel.Malicious => 2.5,
        FindingLevel.Suspicious => 1.0,
        FindingLevel.Benign => -1.5,
        _ => 0
    };
}

/// <summary>Everything the steps may look at for one case. Built from SQL only — no step touches the live site.</summary>
public sealed class InvestigationContext
{
    public required PhishCase Case { get; init; }
    public required Campaign Campaign { get; init; }
    public ProtectedBrand? Brand { get; init; }
    public required IReadOnlyList<ProtectedBrand> Brands { get; init; }
    public required UrlFeatureExtractor Extractor { get; init; }
    /// <summary>The campaign's URLs, highest model score first: (original URL, effective URL after short-link expansion).</summary>
    public required IReadOnlyList<(string Url, string Effective, float Probability)> Urls { get; init; }
    public DomainEvidence? Evidence { get; init; }
    /// <summary>Highest model probability in the campaign (Tier-1 score).</summary>
    public float ModelProbability { get; init; }
    public required ScoreBand ModelBand { get; init; }
    public DateTime NowUtc { get; init; } = DateTime.UtcNow;

    public string RepresentativeUrl => Urls.Count > 0 ? Urls[0].Effective : $"https://{Campaign.RegisteredDomain}/";
    public bool IsBrandOwned => Brands.Any(b => b.OwnedDomains.Split(',', StringSplitOptions.TrimEntries)
        .Contains(Campaign.RegisteredDomain, StringComparer.OrdinalIgnoreCase));
}

/// <summary>One automated analyst step.</summary>
public interface IInvestigationStep
{
    int Number { get; }
    string Name { get; }
    StepFinding Run(InvestigationContext context);
}

public sealed record InvestigationResult(
    long CaseId,
    IReadOnlyList<StepFinding> Steps,
    Verdict Verdict,
    double Confidence,
    string Rationale,
    CaseStatus Status,
    IReadOnlyList<EnforcementNotice> Notices,
    IReadOnlyList<Indicator> Indicators);

/// <summary>Verdict thresholds (Investigation section of appsettings).</summary>
public sealed class InvestigationOptions
{
    /// <summary>Confidence at or above which the case is confirmed as phishing without an analyst.</summary>
    public double PhishingThreshold { get; set; } = 0.95;
    /// <summary>Confidence at or below which the case is dismissed as benign without an analyst.</summary>
    public double BenignThreshold { get; set; } = 0.05;
    /// <summary>Weight of the model score in the verdict (points at probability 0 and 1 are -W and +W).</summary>
    public double ModelWeight { get; set; } = 3.0;
    /// <summary>URLs per campaign to load into the context.</summary>
    public int MaxUrls { get; set; } = 50;
}
