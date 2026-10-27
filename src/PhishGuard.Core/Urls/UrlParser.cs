using System.Net;

namespace PhishGuard.Core.Urls;

public sealed record ParsedUrl(
    string Original,
    string Scheme,
    string Host,          // punycode / ASCII form
    string RegisteredDomain,
    string Subdomain,
    string Tld,           // public suffix, e.g. "com", "co.uk", "web.app"
    string Path,
    string Query,
    bool HasUserInfo,
    bool IsIpHost,
    int? Port);

/// <summary>
/// Offline URL parser. Works without network access (the ground rules forbid touching live sites),
/// using a compact public-suffix table that also covers free-hosting platforms, so that
/// "victim.web.app" registers as its own domain rather than "web.app".
/// Swap for Nager.PublicSuffix with a vendored public_suffix_list.dat if you need full coverage.
/// </summary>
public static class UrlParser
{
    private static readonly HashSet<string> MultiLabelSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "co.uk", "org.uk", "ac.uk", "gov.uk", "com.au", "net.au", "org.au", "co.nz", "co.in", "net.in", "org.in",
        "co.jp", "ne.jp", "com.br", "com.mx", "com.ar", "com.cn", "com.hk", "com.sg", "com.tr", "co.za", "com.ng",
        "co.kr", "com.tw", "com.my", "com.ph", "com.pk", "com.ua", "com.co", "com.pe", "com.vn", "co.id", "or.id",
        // Free-hosting / platform suffixes (private section of the PSL)
        "web.app", "firebaseapp.com", "netlify.app", "vercel.app", "pages.dev", "workers.dev", "github.io",
        "gitlab.io", "herokuapp.com", "azurewebsites.net", "azurestaticapps.net", "blob.core.windows.net",
        "appspot.com", "blogspot.com", "glitch.me", "repl.co", "ngrok-free.app", "ngrok.io", "000webhostapp.com",
        "wixsite.com", "weebly.com", "square.site", "webflow.io", "surge.sh", "onrender.com", "fly.dev",
        "r2.dev", "s3.amazonaws.com", "cloudfront.net", "duckdns.org", "ddns.net", "translate.goog"
    };

    public static bool TryParse(string raw, out ParsedUrl parsed)
    {
        parsed = default!;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var text = raw.Trim();
        if (!text.Contains("://", StringComparison.Ordinal)) text = "http://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)) return false;

        string host;
        try { host = uri.IdnHost.TrimEnd('.').ToLowerInvariant(); }
        catch (UriFormatException) { host = uri.Host.ToLowerInvariant(); }

        var isIp = IPAddress.TryParse(host.Trim('[', ']'), out _);
        var (registered, subdomain, tld) = isIp ? (host, "", "") : SplitHost(host);

        parsed = new ParsedUrl(
            raw, uri.Scheme, host, registered, subdomain, tld,
            uri.AbsolutePath, uri.Query.TrimStart('?'),
            !string.IsNullOrEmpty(uri.UserInfo), isIp,
            uri.IsDefaultPort ? null : uri.Port);
        return true;
    }

    public static (string Registered, string Subdomain, string Tld) SplitHost(string host)
    {
        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length <= 1) return (host, "", "");

        // Longest matching suffix wins (up to 4 labels, e.g. "blob.core.windows.net").
        for (var n = Math.Min(4, labels.Length - 1); n >= 2; n--)
        {
            var suffix = string.Join('.', labels[^n..]);
            if (MultiLabelSuffixes.Contains(suffix))
                return Compose(labels, n);
        }
        return Compose(labels, 1);
    }

    private static (string, string, string) Compose(string[] labels, int suffixLabels)
    {
        var tld = string.Join('.', labels[^suffixLabels..]);
        var regIndex = labels.Length - suffixLabels - 1;
        var registered = labels[regIndex] + "." + tld;
        var sub = regIndex > 0 ? string.Join('.', labels[..regIndex]) : "";
        return (registered, sub, tld);
    }
}
