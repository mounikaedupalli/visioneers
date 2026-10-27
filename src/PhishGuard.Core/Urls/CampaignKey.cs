using System.Text;

namespace PhishGuard.Core.Urls;

/// <summary>
/// Groups URLs that are almost certainly the same attack: same registered domain, same path "shape"
/// (first two segments with digits / random tokens masked) and same impersonated brand.
/// One analyst decision then covers the whole group.
/// </summary>
public static class CampaignKey
{
    public static (string RegisteredDomain, string PathPattern) Parts(string url)
    {
        if (!UrlParser.TryParse(url, out var p)) return ("", "");
        var segments = p.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(Mask);
        return (p.RegisteredDomain, "/" + string.Join('/', segments));
    }

    public static string Build(string band, string registeredDomain, string pathPattern, int? brandId) =>
        $"{band}|{registeredDomain}|{pathPattern}|{brandId?.ToString() ?? "-"}";

    /// <summary>"invoice-83812" → "invoice-#", "Ab3xK9pQ" → "*", "login.php" stays.</summary>
    public static string Mask(string segment)
    {
        var hasDigit = segment.Any(char.IsDigit);
        var hasLetter = segment.Any(char.IsLetter);
        if (segment.Length >= 6 && hasDigit && hasLetter && !segment.Contains('.') && !segment.Contains('-')) return "*";
        if (segment.Length >= 16 && !segment.Contains('.')) return "*";

        var sb = new StringBuilder(segment.Length);
        foreach (var c in segment)
            if (char.IsDigit(c)) { if (sb.Length == 0 || sb[^1] != '#') sb.Append('#'); }
            else sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }
}
