namespace PhishGuard.DataGen;

internal static class WordLists
{
    public static readonly string[] PopularDomains =
    [
        "wikipedia.org", "github.com", "stackoverflow.com", "reddit.com", "nytimes.com", "bbc.co.uk", "cnn.com",
        "espn.com", "imdb.com", "yahoo.com", "bing.com", "duckduckgo.com", "zoom.us", "slack.com", "salesforce.com",
        "oracle.com", "ibm.com", "intel.com", "nvidia.com", "samsung.com", "walmart.com", "target.com", "ebay.com",
        "etsy.com", "booking.com", "expedia.com", "airbnb.com", "uber.com", "spotify.com", "twitch.tv", "medium.com",
        "wordpress.org", "mozilla.org", "python.org", "nuget.org", "npmjs.com", "docker.com", "atlassian.com",
        "notion.so", "trello.com", "hubspot.com", "mailchimp.com", "shopify.com", "stripe.com", "twilio.com",
        "zendesk.com", "cloudflare.com", "akamai.com", "fastly.com", "weather.com", "accuweather.com", "nasa.gov",
        "irs.gov", "who.int", "un.org", "harvard.edu", "mit.edu", "stanford.edu", "coursera.org", "udemy.com",
        "khanacademy.org", "quora.com", "pinterest.com", "tumblr.com", "vimeo.com", "soundcloud.com", "hulu.com",
        "disneyplus.com", "bestbuy.com", "homedepot.com", "lowes.com", "costco.com", "kroger.com", "ikea.com",
        "nike.com", "adidas.com", "zara.com", "hm.com", "sephora.com", "nordstrom.com", "macys.com", "indeed.com",
        "glassdoor.com", "monster.com", "craigslist.org", "zillow.com", "realtor.com", "yelp.com", "tripadvisor.com",
        "reuters.com", "bloomberg.com", "forbes.com", "wsj.com", "theguardian.com", "washingtonpost.com", "cnbc.com",
        "hdfcbank.com", "icicibank.com", "sbi.co.in", "flipkart.com", "infosys.com", "tcs.com", "wipro.com"
    ];

    public static readonly string[] Nouns =
    [
        "river", "summit", "harbor", "maple", "cedar", "pixel", "orbit", "nova", "atlas", "beacon", "canyon", "delta",
        "ember", "falcon", "garden", "horizon", "island", "jasper", "kettle", "lantern", "meadow", "nectar", "oasis",
        "prairie", "quartz", "ridge", "sierra", "timber", "union", "valley", "willow", "yonder", "zenith", "apex",
        "bright", "coastal", "dynamic", "evergreen", "frontier", "global", "heritage", "infinite", "keystone",
        "liberty", "metro", "northern", "pioneer", "quest", "royal", "silver", "trinity", "urban", "vertex", "western",
        "bakery", "dental", "plumbing", "realty", "law", "clinic", "studio", "fitness", "travel", "auto", "pets",
        "farm", "cafe", "books", "design", "media", "labs", "tech", "solutions", "consulting", "group", "partners"
    ];

    public static readonly string[] PathWords =
    [
        "about", "products", "services", "blog", "news", "contact", "careers", "support", "help", "docs", "pricing",
        "category", "article", "events", "team", "faq", "store", "shop", "search", "item", "reviews", "guides",
        "resources", "downloads", "media", "press", "investors", "privacy", "terms", "en-us", "en", "2024", "2025", "2026"
    ];

    public static readonly string[] LureWords =
    [
        "secure", "login", "signin", "verify", "verification", "account", "support", "update", "billing", "help",
        "service", "online", "auth", "id", "center", "confirm", "unlock", "recovery", "alert", "notice", "portal",
        "access", "webscr", "session", "customer", "client", "wallet", "invoice", "delivery", "tracking", "refund"
    ];

    public static readonly string[] LurePaths =
    [
        "login.php", "signin.php", "verify.html", "index.php", "account/update", "secure/auth", "webscr/cmd=login",
        "session/validate", "billing/confirm.php", "recovery/start", "auth/verify.php", "customer/portal/login",
        "office/owa/index.html", "invoice/view.php", "delivery/reschedule.php", "unlock/account.html"
    ];

    public static readonly string[] CheapTlds = ["com", "net", "org", "info", "xyz", "top", "online", "site", "live", "shop", "click", "support", "icu", "cfd", "sbs", "co", "io", "me"];
    public static readonly string[] BenignTlds = ["com", "com", "com", "com", "net", "org", "io", "co", "co.uk", "de", "in", "com.au", "ca", "fr", "nl"];

    public static readonly string[] FreeHostSuffixes =
    [
        "web.app", "firebaseapp.com", "netlify.app", "vercel.app", "pages.dev", "workers.dev", "github.io",
        "herokuapp.com", "azurewebsites.net", "weebly.com", "wixsite.com", "webflow.io", "r2.dev", "glitch.me",
        "ngrok-free.app", "onrender.com", "000webhostapp.com"
    ];

    public static readonly string[] Shorteners = ["bit.ly", "tinyurl.com", "is.gd", "cutt.ly", "rb.gy", "ow.ly", "shorturl.at"];

    public static readonly (string Name, string Domain)[] CheapRegistrars =
    [
        ("NameCheap, Inc.", "namecheap.com"), ("NameSilo, LLC", "namesilo.com"), ("Porkbun LLC", "porkbun.com"),
        ("PDR Ltd. d/b/a PublicDomainRegistry.com", "publicdomainregistry.com"), ("Hostinger Operations, UAB", "hostinger.com"),
        ("GoDaddy.com, LLC", "godaddy.com"), ("Tucows Domains Inc.", "tucows.com"), ("Gname.com Pte. Ltd.", "gname.com")
    ];

    public static readonly (string Name, string Domain)[] CorporateRegistrars =
    [
        ("MarkMonitor Inc.", "markmonitor.com"), ("CSC Corporate Domains, Inc.", "cscglobal.com"),
        ("GoDaddy.com, LLC", "godaddy.com"), ("Tucows Domains Inc.", "tucows.com"), ("Network Solutions, LLC", "networksolutions.com"),
        ("Google LLC", "google.com"), ("Amazon Registrar, Inc.", "amazon.com"), ("NameCheap, Inc.", "namecheap.com")
    ];

    public static readonly (string Name, string Domain)[] HostingProviders =
    [
        ("Cloudflare, Inc.", "cloudflare.com"), ("Amazon Web Services", "amazonaws.com"), ("Microsoft Azure", "microsoft.com"),
        ("Google Cloud", "google.com"), ("DigitalOcean, LLC", "digitalocean.com"), ("Hetzner Online GmbH", "hetzner.com"),
        ("OVH SAS", "ovh.net"), ("Hostinger International", "hostinger.com"), ("Namecheap Hosting", "namecheap.com"),
        ("Akamai (Linode)", "linode.com")
    ];

    public static readonly string[] Countries = ["US", "US", "US", "GB", "DE", "NL", "FR", "IN", "SG", "RU", "CN", "UA", "NG", "BR", "PA", "IS", "CA", "AU"];
    public static readonly string[] BenignCertIssuers = ["DigiCert Inc", "Sectigo Limited", "Let's Encrypt", "GlobalSign nv-sa", "Amazon", "Google Trust Services", "Microsoft Corporation"];
    public static readonly string[] FreeCertIssuers = ["Let's Encrypt", "Let's Encrypt", "ZeroSSL", "Google Trust Services", "cPanel, Inc."];

    /// <summary>Latin → visually identical Cyrillic/Greek letters used for homoglyph attacks.</summary>
    public static readonly Dictionary<char, char[]> Homoglyphs = new()
    {
        ['a'] = ['а', 'α'], ['e'] = ['е'], ['o'] = ['о', 'ο'], ['p'] = ['р'], ['c'] = ['с'], ['y'] = ['у'],
        ['x'] = ['х'], ['i'] = ['і', 'ı'], ['l'] = ['ӏ'], ['d'] = ['ԁ'], ['g'] = ['ɡ'], ['n'] = ['ո'], ['s'] = ['ѕ']
    };

    public static readonly Dictionary<char, string> KeyboardNeighbours = new()
    {
        ['a'] = "qwsz", ['b'] = "vghn", ['c'] = "xdfv", ['d'] = "serfcx", ['e'] = "wsdr", ['f'] = "drtgvc", ['g'] = "ftyhbv",
        ['h'] = "gyujnb", ['i'] = "ujko", ['j'] = "huikmn", ['k'] = "jiolm", ['l'] = "kop", ['m'] = "njk", ['n'] = "bhjm",
        ['o'] = "iklp", ['p'] = "ol", ['q'] = "wa", ['r'] = "edft", ['s'] = "awedxz", ['t'] = "rfgy", ['u'] = "yhji",
        ['v'] = "cfgb", ['w'] = "qase", ['x'] = "zsdc", ['y'] = "tghu", ['z'] = "asx"
    };
}
