namespace PhishGuard.Core.Domain;

/// <summary>Seed list of protected brands. Replace / extend with the sponsor's real portfolio.</summary>
public static class BrandCatalog
{
    public static IReadOnlyList<ProtectedBrand> Seed { get; } =
    [
        B(1,  "PayPal",          "paypal.com",          "paypal",                      "paypal.com,paypal.me,paypalobjects.com"),
        B(2,  "Microsoft",       "microsoft.com",       "microsoft,office365,outlook,onedrive,msft", "microsoft.com,microsoftonline.com,office.com,live.com,outlook.com,sharepoint.com,onedrive.live.com"),
        B(3,  "Apple",           "apple.com",           "apple,icloud,appleid",        "apple.com,icloud.com"),
        B(4,  "Amazon",          "amazon.com",          "amazon,aws,prime",            "amazon.com,amazonaws.com,media-amazon.com"),
        B(5,  "Netflix",         "netflix.com",         "netflix",                     "netflix.com,nflxext.com"),
        B(6,  "Google",          "google.com",          "google,gmail",                "google.com,gmail.com,googleusercontent.com,gstatic.com"),
        B(7,  "Chase",           "chase.com",           "chase,jpmorgan",              "chase.com,jpmorganchase.com"),
        B(8,  "Wells Fargo",     "wellsfargo.com",      "wellsfargo,wells-fargo",      "wellsfargo.com"),
        B(9,  "Bank of America", "bankofamerica.com",   "bankofamerica,bofa",          "bankofamerica.com"),
        B(10, "DHL",             "dhl.com",             "dhl",                         "dhl.com"),
        B(11, "FedEx",           "fedex.com",           "fedex",                       "fedex.com"),
        B(12, "LinkedIn",        "linkedin.com",        "linkedin",                    "linkedin.com,licdn.com"),
        B(13, "Facebook",        "facebook.com",        "facebook,meta,instagram",     "facebook.com,fb.com,instagram.com,meta.com"),
        B(14, "Dropbox",         "dropbox.com",         "dropbox",                     "dropbox.com"),
        B(15, "DocuSign",        "docusign.com",        "docusign",                    "docusign.com,docusign.net"),
        B(16, "Adobe",           "adobe.com",           "adobe",                       "adobe.com"),
        B(17, "Coinbase",        "coinbase.com",        "coinbase",                    "coinbase.com"),
        B(18, "USPS",            "usps.com",            "usps",                        "usps.com"),
    ];

    private static ProtectedBrand B(int id, string name, string domain, string keywords, string owned) =>
        new() { Id = id, Name = name, PrimaryDomain = domain, Keywords = keywords, OwnedDomains = owned };
}
