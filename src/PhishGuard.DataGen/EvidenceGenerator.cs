using PhishGuard.Core.Domain;
using static PhishGuard.DataGen.WordLists;

namespace PhishGuard.DataGen;

/// <summary>
/// Simulates what RDAP/WHOIS, certificate-transparency and a sandboxed page snapshot would return,
/// so agents can gather "evidence" without anyone opening a suspected site.
/// Distributions overlap on purpose (young benign startups, aged compromised sites) so evidence
/// helps but is never a perfect label leak.
/// </summary>
public sealed class EvidenceGenerator(int seed, IReadOnlyList<ProtectedBrand> brands, DateTime asOfUtc)
{
    private readonly Random _rng = new(seed);

    /// <summary>
    /// Hard mode: evidence overlaps more between classes so it helps without giving the label away —
    /// genuine login portals have login forms, some benign pages show a brand logo (resellers, news),
    /// phishing logos are detected less reliably, and more benign domains are newly registered.
    /// </summary>
    public bool Hard { get; init; }

    /// <summary>Benign domains that host real login pages (their snapshot shows a login form).</summary>
    public IReadOnlySet<string> LoginPageDomains { get; init; } = new HashSet<string>();
    private readonly Dictionary<int, ProtectedBrand> _brands = brands.ToDictionary(b => b.Id);
    private readonly HashSet<string> _brandOwned = new(
        brands.SelectMany(b => b.OwnedDomains.Split(',', StringSplitOptions.RemoveEmptyEntries)), StringComparer.OrdinalIgnoreCase);

    private static int Priority(DomainOwner o) => o switch
    {
        DomainOwner.Compromised => 3, DomainOwner.Attacker => 2, DomainOwner.AttackerOnPlatform => 1, _ => 0
    };

    /// <summary>One evidence row per registered domain; the most "malicious" owner seen wins.</summary>
    public IEnumerable<DomainEvidence> Build(IEnumerable<(string RegisteredDomain, DomainOwner Owner, int? BrandId, bool IsIp)> observations)
    {
        var byDomain = new Dictionary<string, (DomainOwner Owner, int? BrandId)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (domain, owner, brandId, isIp) in observations)
        {
            if (isIp) continue;
            // Platform hosts that were abused via a path (sites.google.com, ipfs.io) remain legitimate domains.
            var effective = owner == DomainOwner.AttackerOnPlatform && !IsFreeHostSubdomain(domain) ? DomainOwner.Legit : owner;
            if (_brandOwned.Contains(domain)) effective = DomainOwner.Legit;
            if (!byDomain.TryGetValue(domain, out var existing) || Priority(effective) > Priority(existing.Owner))
                byDomain[domain] = (effective, brandId);
        }

        foreach (var (domain, (owner, brandId)) in byDomain)
            yield return Create(domain, owner, brandId is int id ? _brands.GetValueOrDefault(id) : null);
    }

    private static bool IsFreeHostSubdomain(string registered) =>
        FreeHostSuffixes.Any(s => registered.EndsWith("." + s, StringComparison.OrdinalIgnoreCase)) ||
        registered.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase);

    private DomainEvidence Create(string domain, DomainOwner owner, ProtectedBrand? brand)
    {
        var brandOwned = _brandOwned.Contains(domain);
        var ownerBrand = brandOwned ? brands.First(b => b.OwnedDomains.Contains(domain, StringComparison.OrdinalIgnoreCase)) : null;

        var malicious = owner is DomainOwner.Attacker or DomainOwner.AttackerOnPlatform;
        var pagePhishy = malicious || owner == DomainOwner.Compromised;

        var ageDays = owner switch
        {
            DomainOwner.Attacker => Chance(0.85) ? _rng.Next(0, 60) : _rng.Next(90, 1200),
            DomainOwner.AttackerOnPlatform => _rng.Next(0, 30),
            DomainOwner.Compromised => _rng.Next(700, 6000),
            _ => brandOwned ? _rng.Next(5000, 10000) : Chance(Hard ? 0.15 : 0.1) ? _rng.Next(5, 180) : _rng.Next(365, 8000)
        };

        var (registrar, registrarDomain) = owner switch
        {
            DomainOwner.AttackerOnPlatform => PlatformRegistrar(domain),
            DomainOwner.Attacker => Pick(CheapRegistrars),
            _ => brandOwned ? Pick(CorporateRegistrars[..2]) : Pick(CorporateRegistrars)
        };
        var (host, hostDomain) = Pick(HostingProviders);

        var certIssuer = pagePhishy ? Pick(FreeCertIssuers) : Pick(BenignCertIssuers);
        var certAge = malicious ? _rng.Next(0, 30) : _rng.Next(1, 365);
        var shownBrand = brand ?? ownerBrand;

        var evidence = new DomainEvidence
        {
            RegisteredDomain = domain,
            CreatedOnUtc = asOfUtc.AddDays(-ageDays).Date,
            Registrar = registrar,
            RegistrarAbuseEmail = $"abuse@{registrarDomain}",
            RegistrantCountry = Pick(Countries),
            PrivacyProtected = malicious ? Chance(0.85) : Chance(brandOwned ? 0.0 : 0.3),
            HostingProvider = host,
            HostingAbuseEmail = $"abuse@{hostDomain}",
            CertIssuer = certIssuer,
            CertIssuedOnUtc = asOfUtc.AddDays(-certAge).Date,
            PageTitle = pagePhishy && shownBrand is not null
                ? Pick([$"Sign in - {shownBrand.Name}", $"{shownBrand.Name} | Verify your account", $"{shownBrand.Name} Security Check", $"Log in to your {shownBrand.Name} account", "Account suspended"])
                : brandOwned && ownerBrand is not null ? $"{ownerBrand.Name}" : $"{Capitalize(domain.Split('.')[0])} - {Pick(PathWords)}",
            HasLoginForm = pagePhishy ? Chance(0.92) : LoginPageDomains.Contains(domain) || Chance(brandOwned ? 0.7 : 0.15),
            BrandLogoDetected = pagePhishy ? Chance(Hard ? 0.7 : 0.85) : brandOwned || (Hard && Chance(0.05)),
            ScreenshotRef = $"synthetic://screenshots/{domain}.png"
        };
        ApplySandbox(evidence, pagePhishy, brandOwned, shownBrand);
        return evidence;
    }

    private (string, string) PlatformRegistrar(string domain) =>
        domain.Contains("web.app") || domain.Contains("firebaseapp") || domain.Contains("appspot") ? ("Google LLC", "google.com")
        : domain.Contains("windows.net") || domain.Contains("azurewebsites") ? ("Microsoft Corporation", "microsoft.com")
        : ("Platform-managed subdomain", "platform.invalid");

    /// <summary>
    /// Simulated sandbox detonation (analyst step 4): an isolated browser submits dummy credentials and records what
    /// the page did. Uses its own random stream so the rest of the evidence is unchanged.
    /// </summary>
    private void ApplySandbox(DomainEvidence e, bool pagePhishy, bool brandOwned, ProtectedBrand? brand)
    {
        e.SandboxRunAtUtc = asOfUtc;
        e.BrandPageSimilarity = (float)(brandOwned ? 1.0
            : pagePhishy && brand is not null ? (Hard ? 0.45 : 0.6) + _sandbox.NextDouble() * (Hard ? 0.5 : 0.38)
            : _sandbox.NextDouble() * (Hard ? 0.45 : 0.3));
        if (!e.HasLoginForm) return; // nothing to submit: the credential checks stay null ("not applicable")

        bool S(double p) => _sandbox.NextDouble() < p;
        if (pagePhishy)
        {
            e.SandboxAcceptsAnyCredentials = S(Hard ? 0.75 : 0.85);
            e.SandboxPostsToExternalHost = S(0.6);
            e.SandboxRedirectsToBrand = S(0.45);   // "smart" kits forward the victim to the real site afterwards
        }
        else
        {
            e.SandboxAcceptsAnyCredentials = S(0.02);  // demo / test login pages
            e.SandboxPostsToExternalHost = S(brandOwned ? 0.1 : 0.15); // legitimate SSO / identity providers
            e.SandboxRedirectsToBrand = S(0.01);
        }
        if (e.SandboxPostsToExternalHost == true)
            e.SandboxPostTarget = pagePhishy
                ? SandboxPick(["https://api.telegram.org/bot{token}/sendMessage", "https://{random}.top/gate.php", "https://formsubmit.co/{email}", "https://{random}.workers.dev/collect"])
                : SandboxPick(["https://login.microsoftonline.com/common/oauth2", "https://accounts.google.com/o/oauth2", "https://{tenant}.okta.com/login", "https://auth0.com/u/login"]);
    }

    private readonly Random _sandbox = new(seed + 101);
    private string SandboxPick(string[] items) => items[_sandbox.Next(items.Length)];

    private bool Chance(double p) => _rng.NextDouble() < p;
    private T Pick<T>(IReadOnlyList<T> items) => items[_rng.Next(items.Count)];
    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
