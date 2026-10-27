namespace PhishGuard.Agents;

/// <summary>
/// Phase 4 — the Tier-3 agents (Microsoft Agent Framework on Azure OpenAI). They only ever see Gray-band
/// campaigns, and their tools read SQL Server (DomainEvidence, Brands, Urls) — never the live site.
/// </summary>
public static class AgentCatalog
{
    public const string Evidence = "EvidenceAgent";       // tools: GetDomainEvidence, GetCampaignUrls
    public const string Brand = "BrandAgent";             // tools: GetProtectedBrands, CompareToBrand
    public const string Verdict = "VerdictAgent";         // returns Verdict + confidence + rationale
    public const string Enforcement = "EnforcementAgent"; // picks EnforcementRoute, drafts EnforcementNotice (never sent)
    public const string Monitor = "MonitorAgent";         // schedules MonitorChecks for reactivation

    public static readonly IReadOnlyDictionary<string, string> Instructions = new Dictionary<string, string>
    {
        [Evidence] = "Collect WHOIS/RDAP, certificate and page-snapshot evidence for the campaign's domain from the provided tools. Report facts only.",
        [Brand] = "Decide whether the URLs impersonate a protected brand. Consider typosquats, homoglyphs, brand tokens outside the registered domain, and embedded redirect targets.",
        [Verdict] = "Given the evidence and brand analysis, classify the campaign as Phishing, Benign or NeedsHuman. Give a confidence from 0 to 1 and a short rationale an analyst can verify.",
        [Enforcement] = "For a confirmed phishing campaign choose the best route (Registrar, HostingProvider, Udrp, PlatformAbuse) and draft a concise takedown notice citing the evidence.",
        [Monitor] = "Propose a re-check schedule for a taken-down domain to detect reactivation."
    };
}
