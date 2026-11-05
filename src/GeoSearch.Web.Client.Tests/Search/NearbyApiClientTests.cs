using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Search;
using System.Globalization;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class NearbyApiClientTests
{
    [TestMethod]
    public async Task NearbyUsesFixedWgs84OriginRadiusAndExactCategoryAsync()
    {
        using PendingHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        SearchApiClient api = new(client);
        AppliedNearbyQuery query = new(new NearbyCriteria("Café & bakery", 2000,
            NearbyOriginKind.SelectedPoint, new GeoCoordinate(52.5, 10.1)), new GeoCoordinate(52.374, 9.735));
        CultureInfo old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Task<SearchFetchResult> pending = api.NearbyAsync(query, CancellationToken.None);
            string uri = handler.LastRequest!.RequestUri!.ToString();
            StringAssert.Contains(uri, "api/nearby", StringComparison.Ordinal);
            StringAssert.Contains(uri, "lat=52.374", StringComparison.Ordinal);
            StringAssert.Contains(uri, "lon=9.735", StringComparison.Ordinal);
            StringAssert.Contains(uri, "radius=2000", StringComparison.Ordinal);
            StringAssert.Contains(uri, "limit=20", StringComparison.Ordinal);
            StringAssert.Contains(uri, "category=Caf", StringComparison.Ordinal);
            Assert.IsFalse(uri.Contains("52,374", StringComparison.Ordinal));
            handler.Complete("""{"type":"FeatureCollection","snapshot":"test","features":[]}""");
            Assert.IsInstanceOfType<SearchSuccess>(await pending);
        }
        finally
        {
            CultureInfo.CurrentCulture = old;
        }
    }

    [TestMethod]
    public async Task AllCategoriesOmitsCategoryParameterAsync()
    {
        using PendingHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        SearchApiClient api = new(client);
        Task<SearchFetchResult> pending = api.NearbyAsync(new AppliedNearbyQuery(new NearbyCriteria(),
            new GeoCoordinate(52.3, 9.7)), CancellationToken.None);
        Assert.IsFalse(handler.LastRequest!.RequestUri!.Query.Contains("category", StringComparison.Ordinal));
        handler.Complete("""{"type":"FeatureCollection","snapshot":"test","features":[]}""");
        Assert.IsInstanceOfType<SearchSuccess>(await pending);
    }
}
