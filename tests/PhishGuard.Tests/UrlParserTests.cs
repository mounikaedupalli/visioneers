using PhishGuard.Core.Domain;
using PhishGuard.Core.Urls;

namespace PhishGuard.Tests;

public class UrlParserTests
{
    [Theory]
    [InlineData("https://www.example.com/a/b", "example.com", "www", "com")]
    [InlineData("http://shop.example.co.uk/x", "example.co.uk", "shop", "co.uk")]
    [InlineData("https://my-site-123.web.app/index.html", "my-site-123.web.app", "", "web.app")]
    [InlineData("example.org/path", "example.org", "", "org")]
    public void Splits_registered_domain(string url, string registered, string sub, string tld)
    {
        Assert.True(UrlParser.TryParse(url, out var p));
        Assert.Equal(registered, p.RegisteredDomain);
        Assert.Equal(sub, p.Subdomain);
        Assert.Equal(tld, p.Tld);
    }

    [Fact]
    public void Detects_ip_host_and_userinfo()
    {
        Assert.True(UrlParser.TryParse("http://user@10.1.2.3:8080/x", out var p));
        Assert.True(p.IsIpHost);
        Assert.True(p.HasUserInfo);
        Assert.Equal(8080, p.Port);
    }
}

public class UrlFeatureExtractorTests
{
    private readonly UrlFeatureExtractor _x = new(BrandCatalog.Seed);

    private float F(string url, string name) => _x.Extract(url)[Array.IndexOf(UrlFeatureExtractor.Names, name)];

    [Fact]
    public void Vector_length_matches_names() =>
        Assert.Equal(UrlFeatureExtractor.Names.Length, _x.Extract("https://example.com/").Length);

    [Fact]
    public void Brand_owned_domain_is_not_a_mismatch()
    {
        Assert.Equal(1, F("https://www.paypal.com/signin", "BrandOwnedDomain"));
        Assert.Equal(0, F("https://www.paypal.com/signin", "BrandMismatch"));
    }

    [Fact]
    public void Brand_in_subdomain_of_other_domain_is_a_mismatch()
    {
        Assert.Equal(1, F("https://paypal.example.net/", "BrandInSubdomain"));
        Assert.Equal(1, F("https://paypal.example.net/", "BrandMismatch"));
    }

    [Fact]
    public void Edit_distance_catches_one_letter_typo() =>
        Assert.Equal(1, F("https://paypa1.example/", "MinBrandEditDistance"));
}
