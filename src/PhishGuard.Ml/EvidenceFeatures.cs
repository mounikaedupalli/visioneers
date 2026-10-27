using PhishGuard.Core.Domain;

namespace PhishGuard.Ml;

/// <summary>
/// Features from domain evidence (WHOIS/RDAP, certificate transparency, sandboxed page snapshot). They catch what the URL
/// text can't show, such as a week-old domain with a login form and a copied logo behind an innocent-looking path.
/// Keep <see cref="Names"/> in the same order as <see cref="From"/>.
/// </summary>
public static class EvidenceFeatures
{
    public const int Size = 8;

    public static readonly string[] Names =
    [
        "HasEvidence", "DomainAgeDaysLog", "CertAgeDaysLog", "FreeCertIssuer", "PrivacyProtected",
        "HasLoginForm", "BrandLogoDetected", "NewDomainWithLogin"
    ];

    private static readonly HashSet<string> FreeIssuers = new(StringComparer.OrdinalIgnoreCase)
        { "Let's Encrypt", "ZeroSSL", "cPanel, Inc." };

    /// <param name="seenAtUtc">When the URL was seen; ages are measured from then, not from today.</param>
    public static float[] From(DomainEvidence? e, DateTime seenAtUtc)
    {
        var f = new float[Size];
        if (e is null) return f; // HasEvidence = 0; everything else unknown

        var age = e.CreatedOnUtc is { } created ? Math.Max(0, (seenAtUtc - created).TotalDays) : -1;
        var certAge = e.CertIssuedOnUtc is { } issued ? Math.Max(0, (seenAtUtc - issued).TotalDays) : -1;

        f[0] = 1;
        f[1] = age < 0 ? -1 : (float)Math.Log(1 + age);
        f[2] = certAge < 0 ? -1 : (float)Math.Log(1 + certAge);
        f[3] = e.CertIssuer is not null && FreeIssuers.Contains(e.CertIssuer) ? 1 : 0;
        f[4] = e.PrivacyProtected ? 1 : 0;
        f[5] = e.HasLoginForm ? 1 : 0;
        f[6] = e.BrandLogoDetected ? 1 : 0;
        f[7] = age is >= 0 and < 90 && e.HasLoginForm ? 1 : 0;
        return f;
    }
}
