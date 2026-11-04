using GeoSearch.Web.Shared.Web;
using System.Globalization;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class SimpleUriBuilderTests
{
    [TestMethod]
    public void EncodesQueryValuesAndJoinsPaths()
    {
        Uri uri = SimpleUriBuilder.Create("api/")
            .AppendPath("/search")
            .AppendQuery("q", "König & Kröpcke/?")
            .AppendQuery("limit", 20)
            .BuildUri(UriKind.Relative);

        Assert.AreEqual("api/search?q=K%C3%B6nig%20%26%20Kr%C3%B6pcke%2F%3F&limit=20", uri.OriginalString);
    }

    [TestMethod]
    public void UsesInvariantNumericFormatsEvenUnderGermanLocale()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            string uri = SimpleUriBuilder.Create("api/nearby")
                .AppendQuery("lat", 52.3759)
                .AppendQuery("lon", 9.7320)
                .Build();

            Assert.AreEqual("api/nearby?lat=52.3759&lon=9.732", uri);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void ParserReadsRawQueryPairs()
    {
        SimpleUriParser parsed = SimpleUriParser.Parse("https://example.test/api/search?q=a%26b&limit=10");
        Assert.AreEqual("/api/search", parsed.Path.ToString());
        Assert.IsTrue(parsed.TryGetQueryParameter("q", out RouteDataRef match));
        Assert.AreEqual("a%26b", match.Value.ToString());
        Assert.IsTrue(parsed.ContainsQueryParameter("limit"));
        Assert.IsFalse(parsed.ContainsQueryParameter("missing"));
    }
}
