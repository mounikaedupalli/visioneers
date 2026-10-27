using System.Globalization;
using System.Text;
using PhishGuard.Core.Domain;
using static PhishGuard.DataGen.WordLists;

namespace PhishGuard.DataGen;

/// <summary>Who controls the registered domain — drives the simulated WHOIS/certificate/page evidence.</summary>
public enum DomainOwner { Legit, Attacker, Compromised, AttackerOnPlatform }

/// <param name="Day">Days after the start of the window the URL was seen; null = caller picks a time.</param>
/// <param name="CampaignId">Ground-truth attacker campaign (for checking how well grouping works); null for benign.</param>
/// <param name="ExpandedUrl">For short links: where the link really points (simulated shortener expand API).</param>
/// <param name="ExpandedOwner">Who controls the destination domain (drives its simulated evidence).</param>
public sealed record GeneratedUrl(string Url, UrlCategory Category, int? BrandId, DomainOwner Owner, double? Day = null, int? CampaignId = null,
    string? ExpandedUrl = null, DomainOwner? ExpandedOwner = null);

/// <summary>
/// Generates labelled URLs for every case in <see cref="UrlCategory"/>.
/// Benign generators deliberately include "suspicious-looking but legitimate" URLs (long SSO
/// redirects, SharePoint shares, tracking links) because those drive the sponsor's 90% false positives.
/// Never resolves or requests any URL.
/// </summary>
public sealed class SyntheticUrlGenerator
{
    private readonly Random _rng;
    private readonly int _seed;
    private SyntheticUrlGenerator? _destinations;
    private HashSet<string>? _benignSet;
    private readonly IReadOnlyList<ProtectedBrand> _brands;
    private readonly string[] _benignDomains;
    private readonly IdnMapping _idn = new();
    private readonly Dictionary<UrlCategory, List<AttackCampaign>> _campaigns = [];
    private int _campaignCount;

    /// <summary>Length of the ingestion window in days (campaign lifetimes are placed inside it).</summary>
    public int WindowDays { get; init; } = 14;

    /// <summary>
    /// Chance that a phishing URL starts a new attacker campaign instead of joining an existing one.
    /// 0.15 → 10K phishing URLs come from about 1.5K campaigns, with a few large ones (rich get richer).
    /// </summary>
    public double NewCampaignRate { get; init; } = 0.15;

    /// <summary>Tactics that only appear in the last third of the window (unseen during most of training).</summary>
    public static readonly HashSet<UrlCategory> EmergingTactics = [UrlCategory.Homoglyph, UrlCategory.EncodedLongUrl];

    public int CampaignCount => _campaignCount;

    /// <summary>One attacker operation: a fixed host + phishing-kit path, live for a few hours to a few days.</summary>
    private sealed class AttackCampaign
    {
        public required int Id { get; init; }
        public required GeneratedUrl Template { get; init; }
        public required string VictimParam { get; init; }
        public required double StartDay { get; init; }
        public required double LifeDays { get; init; }
        public int Size { get; set; } = 1;
    }

    public static readonly (UrlCategory Category, int Weight)[] BenignMix =
    [
        (UrlCategory.BenignPlain, 55), (UrlCategory.BenignLongSso, 10), (UrlCategory.BenignSharePoint, 10),
        (UrlCategory.BenignTracking, 12), (UrlCategory.BenignCdn, 8), (UrlCategory.BenignBrandSubdomain, 5)
    ];

    public static readonly (UrlCategory Category, int Weight)[] PhishingMix =
    [
        (UrlCategory.Typosquat, 12), (UrlCategory.Homoglyph, 6), (UrlCategory.Combosquat, 16), (UrlCategory.SubdomainBrand, 12),
        (UrlCategory.PathBrand, 10), (UrlCategory.FreeHosting, 12), (UrlCategory.OpenRedirect, 5), (UrlCategory.UserInfoTrick, 3),
        (UrlCategory.IpHost, 5), (UrlCategory.CompromisedSite, 10), (UrlCategory.EncodedLongUrl, 6), (UrlCategory.Shortener, 3)
    ];

    /// <summary>Extra benign cases in hard mode: they carry signals the normal dataset only gives to phishing.</summary>
    public static readonly (UrlCategory Category, int Weight)[] HardBenignExtras =
    [
        (UrlCategory.BenignLoginPortal, 6), (UrlCategory.BenignLookalikeName, 3), (UrlCategory.BenignFreeHosting, 3)
    ];

    /// <summary>Extra phishing cases in hard mode.</summary>
    public static readonly (UrlCategory Category, int Weight)[] HardPhishingExtras = [(UrlCategory.StealthKit, 10)];

    /// <summary>
    /// Hard mode (--difficulty hard): adds the benign/phishing extras above, gives half of all phishing kits
    /// neutral paths instead of lure words, and rotates some campaigns across many domains (kit reuse).
    /// The normal dataset is unchanged when this is off (same seed → same URLs).
    /// </summary>
    public bool Hard { get; init; }

    public (UrlCategory Category, int Weight)[] BenignCategories => Hard ? [.. BenignMix, .. HardBenignExtras] : BenignMix;
    public (UrlCategory Category, int Weight)[] PhishingCategories => Hard ? [.. PhishingMix, .. HardPhishingExtras] : PhishingMix;

    /// <summary>Campaign types whose kit is copied onto fresh domains in hard mode.</summary>
    private static readonly HashSet<UrlCategory> RotatingKits =
        [UrlCategory.PathBrand, UrlCategory.CompromisedSite, UrlCategory.EncodedLongUrl, UrlCategory.StealthKit];

    public SyntheticUrlGenerator(int seed, IReadOnlyList<ProtectedBrand> brands, IEnumerable<string>? extraBenignDomains = null, int benignDomainPool = 20_000)
    {
        _rng = new Random(seed);
        _seed = seed;
        _brands = brands;
        var pool = new HashSet<string>(PopularDomains, StringComparer.OrdinalIgnoreCase);
        foreach (var d in extraBenignDomains ?? []) pool.Add(d);
        while (pool.Count < benignDomainPool) pool.Add(RandomBusinessDomain());
        _benignDomains = [.. pool];
    }

    public UrlCategory PickCategory((UrlCategory Category, int Weight)[] mix)
    {
        var total = mix.Sum(m => m.Weight);
        var roll = _rng.Next(total);
        foreach (var (c, w) in mix) { if (roll < w) return c; roll -= w; }
        return mix[^1].Category;
    }

    public GeneratedUrl Generate(UrlCategory category) => category switch
    {
        UrlCategory.BenignPlain => Benign(category, $"{Scheme(0.92)}{Www()}{Pick(_benignDomains)}/{(Hard && _rng.NextDouble() < 0.25 ? Pick(NeutralPaths) : Path(0, 4))}{MaybeQuery(0.3)}"),
        UrlCategory.BenignLongSso => Benign(category, LongSso()),
        UrlCategory.BenignSharePoint => Benign(category, ShareLink()),
        UrlCategory.BenignTracking => _rng.NextDouble() < 0.1 ? BenignShortLink() : Benign(category, TrackingLink()),
        UrlCategory.BenignCdn => Benign(category, CdnLink()),
        UrlCategory.BenignBrandSubdomain => BrandOwned(),

        UrlCategory.Typosquat => WithBrand(category, b => $"{Scheme(0.7)}{Www(0.3)}{Typo(Keyword(b))}.{Pick(CheapTlds)}/{KitPath()}{MaybeQuery(0.4)}", DomainOwner.Attacker),
        UrlCategory.Homoglyph => WithBrand(category, b => $"https://{Homoglyph(Keyword(b))}.{Pick(["com", "net", "co", "org"])}/{KitPath()}", DomainOwner.Attacker),
        UrlCategory.Combosquat => WithBrand(category, b => $"{Scheme(0.8)}{Www(0.2)}{Combo(Keyword(b))}.{Pick(CheapTlds)}/{KitPath()}{MaybeQuery(0.4)}", DomainOwner.Attacker),
        UrlCategory.SubdomainBrand => WithBrand(category, b => $"{Scheme(0.8)}{SubdomainTrick(b)}/{KitPath()}", DomainOwner.Attacker),
        UrlCategory.PathBrand => WithBrand(category, b => $"{Scheme(0.6)}{RandomAttackerDomain()}/{Keyword(b)}/{KitPath()}{MaybeQuery(0.5)}", DomainOwner.Attacker),
        UrlCategory.FreeHosting => WithBrand(category, FreeHosted, DomainOwner.AttackerOnPlatform),
        UrlCategory.OpenRedirect => WithBrand(category, OpenRedirect, DomainOwner.Legit),
        UrlCategory.UserInfoTrick => WithBrand(category, b => $"http://{Www(0.5)}{b.PrimaryDomain}{Pick(["", "-secure", "-login"])}@{(_rng.NextDouble() < 0.5 ? Ip() : RandomAttackerDomain())}/{KitPath()}", DomainOwner.Attacker),
        UrlCategory.IpHost => WithBrand(category, b => $"http://{Ip()}{(_rng.NextDouble() < 0.3 ? ":" + Pick(["8080", "8443", "2096", "8888"]) : "")}/{Keyword(b)}/{KitPath()}", DomainOwner.Attacker),
        UrlCategory.CompromisedSite => WithBrand(category, Compromised, DomainOwner.Compromised),
        UrlCategory.EncodedLongUrl => WithBrand(category, EncodedLong, DomainOwner.Attacker),
        UrlCategory.Shortener => ShortenerPhish(),

        // hard mode
        UrlCategory.BenignLoginPortal => Benign(category, $"https://{Pick(["portal", "my", "account", "secure", "login", "app", "members"])}.{Pick(_benignDomains)}/{Pick(BenignLoginPaths)}{MaybeQuery(0.5)}"),
        UrlCategory.BenignLookalikeName => Benign(category, $"{Scheme(0.9)}{Www()}{Pick(LookalikeWords)}{Pick(Nouns)}.{Pick(BenignTlds)}/{Path(0, 3)}{MaybeQuery(0.3)}"),
        UrlCategory.BenignFreeHosting => Benign(category, $"https://{Pick(Nouns)}-{Pick(Nouns)}{(_rng.NextDouble() < 0.5 ? _rng.Next(1, 99).ToString() : "")}.{Pick(FreeHostSuffixes)}/{Path(0, 2)}"),
        UrlCategory.StealthKit => WithBrand(category, _ => $"https://{Www(0.5)}{FreshBusinessDomain()}/{Pick(NeutralPaths)}{MaybeQuery(0.4)}", DomainOwner.Attacker),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Not a synthetic category")
    };

    /// <summary>
    /// A phishing URL that usually belongs to an existing attacker campaign: same registered domain and kit
    /// path, different per-victim token. Campaigns are picked in proportion to their size (a Chinese-restaurant
    /// process), so a handful grow large while most stay small — like real phishing operations.
    /// Each campaign lives for a short window, so a time-based split doesn't leak the same domains into test.
    /// </summary>
    public GeneratedUrl GeneratePhishing(UrlCategory category)
    {
        if (!_campaigns.TryGetValue(category, out var pool)) _campaigns[category] = pool = [];

        if (pool.Count == 0 || _rng.NextDouble() < NewCampaignRate)
        {
            var earliest = EmergingTactics.Contains(category) ? WindowDays * 2.0 / 3 : 0;
            var start = earliest + _rng.NextDouble() * (WindowDays - earliest);
            var campaign = new AttackCampaign
            {
                Id = ++_campaignCount,
                Template = Generate(category),
                VictimParam = Pick(["id", "uid", "session", "ref", "token", "e", "cid", "t"]),
                StartDay = start,
                LifeDays = Math.Min(WindowDays - start, 0.25 + _rng.NextDouble() * 2.75)
            };
            pool.Add(campaign);
            return campaign.Template with { Day = start, CampaignId = campaign.Id };
        }

        var roll = _rng.Next(pool.Sum(c => c.Size));
        var chosen = pool[^1];
        foreach (var c in pool) { if (roll < c.Size) { chosen = c; break; } roll -= c.Size; }
        chosen.Size++;
        return chosen.Template with
        {
            Url = Variant(chosen),
            Day = chosen.StartDay + _rng.NextDouble() * chosen.LifeDays,
            CampaignId = chosen.Id
        };
    }

    /// <summary>
    /// Same host and kit path, new victim: shorteners get a new code, everything else a new tracking token.
    /// In hard mode some kits are also redeployed on a fresh domain (same path), as real kits are.
    /// </summary>
    private string Variant(AttackCampaign c)
    {
        var url = c.Template.Url;
        if (c.Template.Category == UrlCategory.Shortener)
            return url[..(url.LastIndexOf('/') + 1)] + Token(6, 8, alnum: true);

        if (Hard && RotatingKits.Contains(c.Template.Category) && _rng.NextDouble() < 0.3 &&
            Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var freshHost = c.Template.Category == UrlCategory.CompromisedSite ? CompromisedSiteName() : RandomAttackerDomain();
            var at = url.IndexOf(uri.Host, StringComparison.OrdinalIgnoreCase);
            if (at >= 0) url = url[..at] + freshHost + url[(at + uri.Host.Length)..];
        }

        // Hard mode: victim tokens look like ordinary query strings (same names/lengths as benign MaybeQuery).
        var param = Hard ? Pick(["id", "ref", "lang", "page", "session", "src", "q", "sort"]) : c.VictimParam;
        var token = Hard ? Token(2, 12, alnum: true) : Token(8, 20, alnum: true);
        var variant = $"{url}{(url.Contains('?') ? '&' : '?')}{param}={token}";
        return variant.Length > 256 && url.Length <= 256 ? variant[..256] : variant;
    }

    /// <summary>Benign short links too, so "is a shortener" alone can't be a perfect phishing signal.</summary>
    public GeneratedUrl BenignShortLink()
    {
        var link = Benign(UrlCategory.BenignTracking, $"https://{Pick(Shorteners)}/{Token(6, 8, alnum: true)}");
        var dest = Destinations.Generate(Destinations.Pick(BenignShortTargets));
        return link with { ExpandedUrl = dest.Url, ExpandedOwner = DomainOwner.Legit };
    }

    /// <summary>Phishing behind a shortener: the short link is on bit.ly etc., the destination is an ordinary kit.</summary>
    private GeneratedUrl ShortenerPhish()
    {
        var link = WithBrand(UrlCategory.Shortener, _ => $"https://{Pick(Shorteners)}/{Token(6, 8, alnum: true)}", DomainOwner.Legit);
        var dest = Destinations.Generate(Destinations.Pick(Hard ? [.. PhishShortTargets, UrlCategory.StealthKit] : PhishShortTargets));
        return link with { BrandId = dest.BrandId, ExpandedUrl = dest.Url, ExpandedOwner = dest.Owner };
    }

    private static readonly UrlCategory[] PhishShortTargets =
    [
        UrlCategory.Combosquat, UrlCategory.Typosquat, UrlCategory.SubdomainBrand, UrlCategory.PathBrand,
        UrlCategory.FreeHosting, UrlCategory.CompromisedSite
    ];

    private static readonly UrlCategory[] BenignShortTargets =
        [UrlCategory.BenignPlain, UrlCategory.BenignPlain, UrlCategory.BenignSharePoint, UrlCategory.BenignCdn];

    /// <summary>
    /// Separate generator (own seed, same benign domain pool) for short-link destinations, so adding
    /// expansions doesn't shift the main random sequence: the same seed still gives the same URLs.
    /// </summary>
    private SyntheticUrlGenerator Destinations => _destinations ??=
        new SyntheticUrlGenerator(_seed + 7919, _brands, _benignDomains, _benignDomains.Length) { Hard = Hard, WindowDays = WindowDays };

    // ---------------- benign ----------------

    private GeneratedUrl Benign(UrlCategory c, string url) => new(url, c, null, DomainOwner.Legit);

    private GeneratedUrl BrandOwned()
    {
        var b = Pick(_brands);
        var owned = b.OwnedDomains.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var domain = Pick(owned);
        var sub = Pick(["www", "login", "account", "signin", "secure", "support", "help", "my", "id", "auth"]);
        var path = Pick(["signin", "login", "account/security", "myaccount/settings", "help/contact", "verify", "billing/history", "security/check"]);
        return new($"https://{sub}.{domain}/{path}{MaybeQuery(0.4)}", UrlCategory.BenignBrandSubdomain, b.Id, DomainOwner.Legit);
    }

    private string LongSso()
    {
        var app = $"https%3A%2F%2F{Pick(["app", "portal", "my", "hr", "crm"])}.{Pick(_benignDomains)}%2Fsignin-oidc";
        return Pick<Func<string>>(
        [
            () => $"https://login.microsoftonline.com/{Pick(["common", "organizations", Guid()])}/oauth2/v2.0/authorize?client_id={Guid()}&response_type=code&redirect_uri={app}&scope=openid%20profile%20email&state={Token(32, 48)}&nonce={Token(24, 32)}",
            () => $"https://accounts.google.com/o/oauth2/v2/auth?client_id={Digits(12)}-{Token(32, 32, alnum: true).ToLowerInvariant()}.apps.googleusercontent.com&redirect_uri={app}&response_type=code&scope=openid%20email&state={Token(24, 40)}",
            () => $"https://{Pick(Nouns)}{Pick(Nouns)}.okta.com/oauth2/default/v1/authorize?client_id={Token(20, 20, alnum: true)}&redirect_uri={app}&response_type=code&state={Token(20, 30)}",
            () => $"https://appleid.apple.com/auth/authorize?client_id=com.{Pick(Nouns)}.{Pick(Nouns)}&redirect_uri={app}&response_type=code%20id_token&state={Token(20, 30)}&response_mode=form_post"
        ])();
    }

    private string ShareLink() => Pick<Func<string>>(
    [
        () => $"https://{Pick(Nouns)}{Pick(Nouns)}.sharepoint.com/:{Pick(["w", "x", "p", "b", "f"])}:/r/sites/{Pick(Nouns)}/_layouts/15/Doc.aspx?sourcedoc=%7B{Guid().ToUpperInvariant()}%7D&file={Pick(Nouns)}-{Pick(PathWords)}.docx&action=default&mobileredirect=true",
        () => $"https://{Pick(Nouns)}-my.sharepoint.com/:b:/g/personal/{Pick(Nouns)}_{Pick(Nouns)}_com/{Token(40, 46)}?e={Token(6, 6, alnum: true)}",
        () => $"https://1drv.ms/{Pick(["b", "w", "x", "f"])}/s!{Token(30, 34)}",
        () => $"https://drive.google.com/file/d/{Token(28, 33)}/view?usp=sharing",
        () => $"https://docs.google.com/{Pick(["document", "spreadsheets", "presentation"])}/d/{Token(40, 44)}/edit?usp=sharing",
        () => $"https://www.dropbox.com/scl/fi/{Token(20, 25, alnum: true).ToLowerInvariant()}/{Pick(Nouns)}.pdf?rlkey={Token(25, 25, alnum: true).ToLowerInvariant()}&dl=0"
    ])();

    private string TrackingLink()
    {
        var d = Pick(_benignDomains);
        return Pick<Func<string>>(
        [
            () => $"https://click.{d}/?qs={Hex(64, 96)}",
            () => $"https://links.{d}/ls/click?upn={Token(80, 160)}",
            () => $"https://email.mg.{d}/c/{Token(60, 120)}",
            () => $"https://{d}/track/click?u={Hex(24, 24)}&id={Hex(10, 10)}&e={Hex(10, 10)}",
            () => $"https://www.{d}/{Path(1, 2)}?utm_source=newsletter&utm_medium=email&utm_campaign={Pick(Nouns)}_{_rng.Next(2023, 2027)}&utm_content={Token(8, 12)}"
        ])();
    }

    private string CdnLink() => Pick<Func<string>>(
    [
        () => $"https://cdn.{Pick(_benignDomains)}/assets/{Hex(8, 8)}/{Pick(["app", "main", "vendor", "runtime"])}.{Hex(8, 8)}.{Pick(["js", "css"])}",
        () => $"https://d{Token(12, 13, alnum: true).ToLowerInvariant()}.cloudfront.net/{Path(1, 3)}/{Hex(16, 32)}.{Pick(["png", "jpg", "woff2", "js"])}",
        () => $"https://{Pick(Nouns)}-{Pick(Nouns)}-assets.s3.amazonaws.com/{Path(1, 3)}/{Hex(32, 32)}.{Pick(["png", "pdf", "zip"])}",
        () => $"https://static.{Pick(_benignDomains)}/images/{_rng.Next(2019, 2027)}/{_rng.Next(1, 13):D2}/{Pick(Nouns)}-{Hex(6, 6)}.webp"
    ])();

    // ---------------- phishing ----------------

    private GeneratedUrl WithBrand(UrlCategory c, Func<ProtectedBrand, string> build, DomainOwner owner)
    {
        var b = Pick(_brands);
        return new(build(b), c, b.Id, owner);
    }

    private string Typo(string word)
    {
        var chars = word.ToCharArray().ToList();
        var pos = _rng.Next(chars.Count);
        string result;
        switch (_rng.Next(7))
        {
            case 0 when chars.Count > 4: chars.RemoveAt(pos); result = new string([.. chars]); break;          // omission
            case 1: chars.Insert(pos, chars[pos]); result = new string([.. chars]); break;                      // repetition
            case 2 when pos < chars.Count - 1:                                                                   // transposition
                (chars[pos], chars[pos + 1]) = (chars[pos + 1], chars[pos]); result = new string([.. chars]); break;
            case 3 when KeyboardNeighbours.TryGetValue(chars[pos], out var n):                                   // fat finger
                chars[pos] = n[_rng.Next(n.Length)]; result = new string([.. chars]); break;
            case 4: result = word.Replace("m", "rn").Replace("w", "vv"); break;                                 // rn→m, vv→w
            case 5: result = word.Contains('l') ? word.Replace('l', '1') : word.Replace('o', '0'); break;      // digit swap
            default: chars.Insert(pos, (char)('a' + _rng.Next(26))); result = new string([.. chars]); break;   // insertion
        }
        return result == word ? word + Pick(["s", "e", "x"]) : result;
    }

    private string Homoglyph(string word)
    {
        var chars = word.ToCharArray();
        var candidates = Enumerable.Range(0, chars.Length).Where(i => Homoglyphs.ContainsKey(chars[i])).ToList();
        if (candidates.Count == 0) return _idn.GetAscii(word + "ѕ");
        var swaps = Math.Min(candidates.Count, _rng.Next(1, 3));
        foreach (var i in candidates.OrderBy(_ => _rng.Next()).Take(swaps))
            chars[i] = Pick(Homoglyphs[chars[i]]);
        return _idn.GetAscii(new string(chars)); // appears in logs as xn--...
    }

    private string Combo(string brand) => _rng.Next(4) switch
    {
        0 => $"{brand}-{Pick(LureWords)}",
        1 => $"{brand}-{Pick(LureWords)}-{Pick(LureWords)}",
        2 => $"{Pick(LureWords)}-{brand}",
        _ => $"{Pick(LureWords)}{brand}{Pick(["", "online", "help", _rng.Next(10, 999).ToString()])}"
    };

    private string SubdomainTrick(ProtectedBrand b) => _rng.Next(4) switch
    {
        0 => $"{b.PrimaryDomain}.{Pick(LureWords)}-{Pick(LureWords)}.{Pick(CheapTlds)}",
        1 => $"{Keyword(b)}.com-{Pick(LureWords)}.{Pick(CheapTlds)}",
        2 => $"{Pick(["login", "secure", "account", "www"])}.{Keyword(b)}.{RandomAttackerDomain()}",
        _ => $"{Keyword(b)}-{Pick(LureWords)}.{Pick(Nouns)}{_rng.Next(10, 99)}.{Pick(CheapTlds)}"
    };

    private string FreeHosted(ProtectedBrand b)
    {
        var k = Keyword(b);
        return _rng.Next(5) switch
        {
            0 => $"https://sites.google.com/view/{k}-{Pick(LureWords)}-{Token(4, 6, alnum: true).ToLowerInvariant()}",
            1 => $"https://{Pick(Nouns)}{_rng.Next(100, 9999)}.blob.core.windows.net/$web/{k}/{Pick(["index.html", "login.html", "verify.html"])}",
            2 => $"https://ipfs.io/ipfs/{Token(46, 46, alnum: true)}?filename={k}-{Pick(LureWords)}.html",
            _ => $"https://{k}-{Pick(LureWords)}-{Token(4, 6, alnum: true).ToLowerInvariant()}.{Pick(FreeHostSuffixes)}/{KitPath()}"
        };
    }

    private string OpenRedirect(ProtectedBrand b)
    {
        var target = Uri.EscapeDataString($"https://{Combo(Keyword(b))}.{Pick(CheapTlds)}/{KitPath()}");
        return _rng.Next(4) switch
        {
            0 => $"https://www.google.com/url?q={target}&sa=D&source=editors&ust={Digits(16)}&usg={Token(28, 28)}",
            1 => $"https://www.{Pick(_benignDomains)}/redirect?url={target}",
            2 => $"https://{Pick(_benignDomains)}/out?{Pick(["goto", "dest", "next", "u"])}={target}",
            _ => $"https://l.facebook.com/l.php?u={target}&h={Token(30, 40)}"
        };
    }

    private string Compromised(ProtectedBrand b)
    {
        var site = CompromisedSiteName();
        var k = Keyword(b);
        return _rng.Next(4) switch
        {
            0 => $"https://{Www(0.6)}{site}/wp-content/plugins/{Pick(Nouns)}/{k}/{KitPath()}",
            1 => $"https://{Www(0.6)}{site}/wp-includes/js/{Token(6, 10, alnum: true).ToLowerInvariant()}/{k}/index.php",
            2 => $"https://{Www(0.6)}{site}/.well-known/pki-validation/{k}/{KitPath()}",
            _ => $"http://{site}/images/{Pick(PathWords)}/{k}-{Pick(LureWords)}/{KitPath()}"
        };
    }

    private string EncodedLong(ProtectedBrand b)
    {
        var email = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Pick(Nouns)}.{Pick(Nouns)}@{Pick(_benignDomains)}"));
        var host = _rng.NextDouble() < 0.5 ? $"{Combo(Keyword(b))}.{Pick(CheapTlds)}" : RandomAttackerDomain();
        var sb = new StringBuilder($"https://{host}/{Token(30, 60)}/{KitPath()}?{Pick(["email", "e", "login_hint", "user"])}={email}&token={Token(20, 40)}");
        var targetLength = _rng.Next(190, 257);
        while (sb.Length < targetLength) sb.Append($"&{Pick(["sid", "r", "k", "x", "data"])}={Token(8, 24)}");
        return sb.Length > 256 ? sb.ToString(0, 256) : sb.ToString();
    }

    // ---------------- helpers ----------------

    /// <summary>Kit landing path: always a lure path normally; in hard mode half the kits use a neutral path.</summary>
    private string KitPath() => Hard && _rng.NextDouble() < 0.5 ? Pick(NeutralPaths) : Pick(LurePaths);

    private string CompromisedSiteName() => $"{Pick(Nouns)}{Pick(Nouns)}{Pick(["", "llc", "co", "inc"])}.{Pick(BenignTlds)}";

    private string Keyword(ProtectedBrand b) => b.Keywords.Split(',')[_rng.NextDouble() < 0.8 ? 0 : _rng.Next(b.Keywords.Split(',').Length)].Trim();

    /// <summary>A business-looking domain the attacker registered: never one of the benign pool's real sites.</summary>
    private string FreshBusinessDomain()
    {
        _benignSet ??= new HashSet<string>(_benignDomains, StringComparer.OrdinalIgnoreCase);
        string d;
        do d = RandomBusinessDomain(); while (_benignSet.Contains(d));
        return d;
    }

    public string RandomBusinessDomain() => $"{Pick(Nouns)}{Pick(Nouns)}{(_rng.NextDouble() < 0.3 ? _rng.Next(1, 99).ToString() : "")}.{Pick(BenignTlds)}";

    private string RandomAttackerDomain() => _rng.Next(3) switch
    {
        0 => $"{Token(6, 12, alnum: true).ToLowerInvariant()}.{Pick(CheapTlds)}",
        1 => $"{Pick(Nouns)}-{Pick(LureWords)}{_rng.Next(1, 999)}.{Pick(CheapTlds)}",
        _ => $"{Pick(Nouns)}{Pick(Nouns)}.{Pick(CheapTlds)}"
    };

    private string Scheme(double httpsProbability) => _rng.NextDouble() < httpsProbability ? "https://" : "http://";
    private string Www(double p = 0.6) => _rng.NextDouble() < p ? "www." : "";
    private string Ip() => $"{_rng.Next(11, 223)}.{_rng.Next(0, 256)}.{_rng.Next(0, 256)}.{_rng.Next(1, 255)}";

    private string Path(int min, int max) => string.Join('/', Enumerable.Range(0, _rng.Next(min, max + 1))
        .Select(_ => _rng.NextDouble() < 0.2 ? $"{Pick(Nouns)}-{Pick(PathWords)}-{_rng.Next(100, 99999)}" : Pick(PathWords)));

    private string MaybeQuery(double p) => _rng.NextDouble() >= p ? "" :
        "?" + string.Join('&', Enumerable.Range(0, _rng.Next(1, 4)).Select(_ => $"{Pick(["id", "ref", "lang", "page", "session", "src", "q", "sort"])}={Token(2, 12, alnum: true)}"));

    private string Guid()
    {
        var b = new byte[16];
        _rng.NextBytes(b);
        return new System.Guid(b).ToString();
    }

    private string Digits(int n) => string.Concat(Enumerable.Range(0, n).Select(_ => (char)('0' + _rng.Next(10))));
    private string Hex(int min, int max) => string.Concat(Enumerable.Range(0, _rng.Next(min, max + 1)).Select(_ => "0123456789abcdef"[_rng.Next(16)]));

    private string Token(int min, int max, bool alnum = false)
    {
        const string a = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        const string b64 = a + "-_";
        var set = alnum ? a : b64;
        return string.Concat(Enumerable.Range(0, _rng.Next(min, max + 1)).Select(_ => set[_rng.Next(set.Length)]));
    }

    public T Pick<T>(IReadOnlyList<T> items) => items[_rng.Next(items.Count)];
}
