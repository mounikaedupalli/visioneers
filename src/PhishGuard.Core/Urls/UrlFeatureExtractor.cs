using PhishGuard.Core.Domain;

namespace PhishGuard.Core.Urls;

/// <summary>
/// Turns a URL into a fixed-length numeric vector for ML.NET. Two families of features:
///  - lexical/structural (what the current XGBoost model already sees), and
///  - brand-aware (does a protected brand appear OUTSIDE the registered domain, how close is the
///    domain to a brand) — the signal that separates "looks odd" from "impersonates someone".
/// Keep <see cref="Names"/> in the same order as <see cref="Extract"/>.
/// </summary>
public sealed class UrlFeatureExtractor
{
    public static readonly string[] Names =
    [
        "UrlLength", "HostLength", "PathLength", "QueryLength", "DotsInHost", "SubdomainDepth",
        "HyphensInHost", "DigitsInHost", "DigitRatioUrl", "SpecialCharCount", "PercentEncodedCount",
        "UrlEntropy", "HostEntropy", "IsIpHost", "HasUserInfo", "HasPort", "IsHttps", "IsPunycode",
        "SuspiciousTld", "FreeHostingSuffix", "IsShortener", "HasRedirectParam", "EmbeddedUrlInQuery",
        "LongestTokenLength", "LoginKeywordCount", "PathDepth", "HasPhpOrHtmlFile", "HasWpPath",
        "Base64LikeToken",
        "BrandInRegisteredDomain", "BrandInSubdomain", "BrandInPath", "BrandOwnedDomain",
        "MinBrandEditDistance", "BrandEditDistanceRatio", "BrandWithHyphenWords", "HomoglyphMapsToBrand",
        "BrandMismatch", "TldIsCom", "VowelRatioDomain", "MixedScriptLabel", "SkeletonBrandDistance"
    ];

    public static int Count => Names.Length;

    /// <summary>The first N features are lexical/structural (what the sponsor's current model sees); the rest are brand-aware.</summary>
    public const int LexicalCount = 29;

    private static readonly HashSet<string> SuspiciousTlds = new(StringComparer.OrdinalIgnoreCase)
    { "xyz", "top", "club", "online", "site", "info", "live", "icu", "buzz", "rest", "shop", "click", "link",
      "work", "support", "tk", "ml", "ga", "cf", "gq", "zip", "mov", "cyou", "sbs", "cfd" };

    private static readonly HashSet<string> FreeHosting = new(StringComparer.OrdinalIgnoreCase)
    { "web.app", "firebaseapp.com", "netlify.app", "vercel.app", "pages.dev", "workers.dev", "github.io",
      "herokuapp.com", "azurewebsites.net", "blob.core.windows.net", "appspot.com", "blogspot.com", "glitch.me",
      "ngrok-free.app", "ngrok.io", "000webhostapp.com", "wixsite.com", "weebly.com", "webflow.io", "r2.dev",
      "onrender.com", "duckdns.org", "ddns.net", "translate.goog" };

    private static readonly HashSet<string> Shorteners = new(StringComparer.OrdinalIgnoreCase)
    { "bit.ly", "tinyurl.com", "t.co", "goo.gl", "ow.ly", "is.gd", "buff.ly", "cutt.ly", "rb.gy", "shorturl.at", "tiny.cc" };

    private static readonly string[] LoginWords =
    [ "login", "signin", "sign-in", "logon", "verify", "verification", "account", "secure", "update", "confirm",
      "password", "billing", "unlock", "suspended", "wallet", "auth", "session", "webscr", "recovery" ];

    private static readonly string[] RedirectParams = [ "url=", "redirect=", "redirect_uri=", "next=", "u=", "q=", "dest=", "continue=", "target=", "goto=" ];

    private readonly List<(ProtectedBrand Brand, string[] Keywords, HashSet<string> Owned)> _brands;

    public UrlFeatureExtractor(IEnumerable<ProtectedBrand> brands)
    {
        _brands = brands.Select(b => (b,
            b.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            new HashSet<string>(b.OwnedDomains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    public float[] Extract(string url)
    {
        var f = new float[Count];
        if (!UrlParser.TryParse(url, out var p)) { f[0] = url.Length; return f; }

        var lower = url.ToLowerInvariant();
        var hostLabel = p.RegisteredDomain.Split('.')[0];
        var i = 0;

        f[i++] = url.Length;
        f[i++] = p.Host.Length;
        f[i++] = p.Path.Length;
        f[i++] = p.Query.Length;
        f[i++] = p.Host.Count(c => c == '.');
        f[i++] = p.Subdomain.Length == 0 ? 0 : p.Subdomain.Split('.').Length;
        f[i++] = p.Host.Count(c => c == '-');
        f[i++] = p.Host.Count(char.IsDigit);
        f[i++] = url.Length == 0 ? 0 : (float)url.Count(char.IsDigit) / url.Length;
        f[i++] = url.Count(c => "@~_=&%!$*+,;".Contains(c));
        f[i++] = CountOccurrences(url, "%");
        f[i++] = Entropy(url);
        f[i++] = Entropy(p.Host);
        f[i++] = p.IsIpHost ? 1 : 0;
        f[i++] = p.HasUserInfo || url.Contains('@') ? 1 : 0;
        f[i++] = p.Port.HasValue ? 1 : 0;
        f[i++] = p.Scheme == "https" ? 1 : 0;
        f[i++] = p.Host.Contains("xn--", StringComparison.Ordinal) ? 1 : 0;
        f[i++] = SuspiciousTlds.Contains(p.Tld.Split('.')[^1]) ? 1 : 0;
        f[i++] = FreeHosting.Contains(p.Tld) ? 1 : 0;
        f[i++] = Shorteners.Contains(p.RegisteredDomain) ? 1 : 0;
        f[i++] = RedirectParams.Any(r => p.Query.Contains(r, StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
        f[i++] = p.Query.Contains("http", StringComparison.OrdinalIgnoreCase) || p.Query.Contains("%2f%2f", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        f[i++] = Tokens(lower).DefaultIfEmpty("").Max(t => t.Length);
        f[i++] = LoginWords.Count(w => lower.Contains(w, StringComparison.Ordinal));
        f[i++] = p.Path.Count(c => c == '/');
        f[i++] = p.Path.EndsWith(".php", StringComparison.OrdinalIgnoreCase) || p.Path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || p.Path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        f[i++] = lower.Contains("/wp-") ? 1 : 0;
        f[i++] = Tokens(url).Any(LooksBase64) ? 1 : 0;

        // ---- brand-aware ----
        var owned = _brands.Any(b => b.Owned.Contains(p.RegisteredDomain));
        var inRegistered = _brands.Any(b => b.Keywords.Any(k => hostLabel.Contains(k, StringComparison.OrdinalIgnoreCase)));
        var inSub = _brands.Any(b => b.Keywords.Any(k => p.Subdomain.Contains(k, StringComparison.OrdinalIgnoreCase)));
        var inPath = _brands.Any(b => b.Keywords.Any(k => (p.Path + "?" + p.Query).Contains(k, StringComparison.OrdinalIgnoreCase)));
        var (minDist, ratio) = ClosestBrand(hostLabel);
        var unicodeLabel = ToUnicode(p.RegisteredDomain).Split('.')[0]; // registered label, not "www"
        var skeleton = Confusables.Skeleton(unicodeLabel);
        var homoglyphHit = (Confusables.HasConfusable(unicodeLabel) || skeleton != unicodeLabel.ToLowerInvariant()) &&
                           _brands.Any(b => b.Keywords.Any(k => skeleton.Contains(k, StringComparison.Ordinal)));
        var (skeletonDist, _) = ClosestBrand(skeleton);

        f[i++] = inRegistered ? 1 : 0;
        f[i++] = inSub ? 1 : 0;
        f[i++] = inPath ? 1 : 0;
        f[i++] = owned ? 1 : 0;
        f[i++] = minDist;
        f[i++] = ratio;
        f[i++] = inRegistered && hostLabel.Contains('-') ? 1 : 0;
        f[i++] = homoglyphHit ? 1 : 0;
        f[i++] = (inRegistered || inSub || inPath || homoglyphHit || minDist is > 0 and <= 2) && !owned ? 1 : 0;
        f[i++] = p.Tld == "com" ? 1 : 0;
        f[i++] = hostLabel.Length == 0 ? 0 : (float)hostLabel.Count(c => "aeiou".Contains(c)) / hostLabel.Length;
        f[i++] = Confusables.IsMixedScript(unicodeLabel) ? 1 : 0;
        f[i++] = owned ? 99 : Math.Min(skeletonDist, 10);

        return f;
    }

    /// <summary>
    /// Best guess at the brand a URL impersonates, from the URL alone (no ground truth): brand keyword in the
    /// host/path/query, a homoglyph that normalises to a keyword, or a registered label within edit distance 2.
    /// Returns null for the brand's own domains.
    /// </summary>
    public int? DetectBrand(string url)
    {
        if (!UrlParser.TryParse(url, out var p)) return null;
        var hostLabel = p.RegisteredDomain.Split('.')[0];
        var normalized = Confusables.Skeleton(ToUnicode(p.RegisteredDomain).Split('.')[0]);
        var rest = Uri.UnescapeDataString(p.Subdomain + "/" + p.Path + "?" + p.Query);

        foreach (var (brand, keywords, owned) in _brands)
        {
            if (owned.Contains(p.RegisteredDomain) && !rest.Contains("http", StringComparison.OrdinalIgnoreCase)) continue;
            if (keywords.Any(k => hostLabel.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                  normalized.Contains(k, StringComparison.Ordinal) ||
                                  rest.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return brand.Id;
        }

        ProtectedBrand? best = null;
        var bestDist = int.MaxValue;
        foreach (var (brand, keywords, _) in _brands)
            foreach (var k in keywords.Where(k => k.Length >= 4))
            {
                var d = Levenshtein(hostLabel, k);
                if (d < bestDist) { bestDist = d; best = brand; }
            }
        return bestDist is > 0 and <= 2 ? best?.Id : null;
    }

    private (float Dist, float Ratio) ClosestBrand(string label)
    {
        var best = int.MaxValue;
        var bestLen = 1;
        foreach (var (_, keywords, _) in _brands)
            foreach (var k in keywords)
            {
                var d = Levenshtein(label, k);
                if (d < best) { best = d; bestLen = k.Length; }
            }
        return best == int.MaxValue ? (99, 1) : (best, (float)best / bestLen);
    }

    private static string ToUnicode(string host)
    {
        try { return new System.Globalization.IdnMapping().GetUnicode(host); }
        catch (ArgumentException) { return host; }
    }

    private static IEnumerable<string> Tokens(string s) =>
        s.Split(['/', '?', '&', '=', '.', '-', '_', ':', '#', '@'], StringSplitOptions.RemoveEmptyEntries);

    private static bool LooksBase64(string t) =>
        t.Length >= 24 && t.All(c => char.IsLetterOrDigit(c) || c is '+' or '%') && t.Any(char.IsUpper) && t.Any(char.IsDigit);

    private static int CountOccurrences(string s, string sub)
    {
        int count = 0, idx = 0;
        while ((idx = s.IndexOf(sub, idx, StringComparison.Ordinal)) >= 0) { count++; idx += sub.Length; }
        return count;
    }

    public static float Entropy(string s)
    {
        if (s.Length == 0) return 0;
        var counts = new Dictionary<char, int>();
        foreach (var c in s) counts[c] = counts.GetValueOrDefault(c) + 1;
        double h = 0;
        foreach (var n in counts.Values) { var p = (double)n / s.Length; h -= p * Math.Log2(p); }
        return (float)h;
    }

    public static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }
}
