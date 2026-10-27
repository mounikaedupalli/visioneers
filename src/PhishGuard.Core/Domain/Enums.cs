namespace PhishGuard.Core.Domain;

/// <summary>
/// The case taxonomy. Every URL in the dataset carries exactly one category so that
/// recall and false positives can be reported per attack type, not just overall.
/// </summary>
public enum UrlCategory
{
    // ---- Benign (Label = false) ----
    BenignPlain = 0,          // ordinary site pages
    BenignLongSso = 1,        // long OAuth / SSO redirect chains on real identity providers
    BenignSharePoint = 2,     // SharePoint / OneDrive / Google Drive share links
    BenignTracking = 3,       // marketing / email click-tracking links
    BenignCdn = 4,            // CDN asset URLs with hashes
    BenignBrandSubdomain = 5, // a brand's own real subdomains (login.microsoftonline.com ...)
    // Hard mode (--difficulty hard): benign URLs that carry the signals phishing usually has
    BenignLoginPortal = 6,    // real login/billing/verify pages on ordinary sites (portal.smallbiz.com/account/login.php)
    BenignLookalikeName = 7,  // unrelated businesses whose name contains a brand word (applegrovefarm.com, chasebakery.net)
    BenignFreeHosting = 8,    // legitimate projects on free hosting (my-portfolio.github.io, team-docs.web.app)

    // ---- Phishing (Label = true) ----
    Typosquat = 100,          // paypa1.com, amazom.com
    Homoglyph = 101,          // Cyrillic / Greek look-alike letters, punycode xn--
    Combosquat = 102,         // paypal-secure-verify.com
    SubdomainBrand = 103,     // paypal.com.account-check.xyz
    PathBrand = 104,          // evil.top/paypal/signin.php
    FreeHosting = 105,        // *.web.app, *.netlify.app, sites.google.com/...
    OpenRedirect = 106,       // legit.com/redirect?url=<phish>
    UserInfoTrick = 107,      // https://bank.com@1.2.3.4/login
    IpHost = 108,             // http://185.22.10.4/secure/login
    CompromisedSite = 109,    // oldsmallbiz.com/wp-includes/.../office365/
    EncodedLongUrl = 110,     // base64 / percent-encoded payloads padded to ~256 chars
    Shortener = 111,          // bit.ly-style short links (label taken from ground truth)
    StealthKit = 112,         // hard mode: neutral-looking domain and path, no brand or lure words in the URL at all
    FeedReal = 199            // imported from a public threat feed (real-world sample)
}

public enum UrlSource { Synthetic = 0, OpenPhish = 1, UrlHaus = 2, PhishStats = 3, Tranco = 4, Manual = 9 }

public enum ScoreBand { Clear = 0, Gray = 1, Confirmed = 2 }

public enum CaseStatus { Open = 0, AgentReview = 1, AnalystReview = 2, Confirmed = 3, Dismissed = 4, TakedownFiled = 5, Closed = 6, Reactivated = 7 }

public enum Verdict { Unknown = 0, Phishing = 1, Benign = 2, NeedsHuman = 3 }

public enum EnforcementRoute { Registrar = 0, HostingProvider = 1, Udrp = 2, PlatformAbuse = 3, CertificateAuthority = 4 }

public enum IndicatorType { Domain = 0, Url = 1, Ip = 2 }

/// <summary>Outcome of one automated investigation step.</summary>
public enum FindingLevel { Unknown = 0, Benign = 1, Suspicious = 2, Malicious = 3 }

public enum NoticeStatus { Draft = 0, Filed = 1, Acknowledged = 2, Resolved = 3, Rejected = 4 }

public static class UrlCategoryExtensions
{
    public static bool IsPhishing(this UrlCategory c) => (int)c >= 100;
}
