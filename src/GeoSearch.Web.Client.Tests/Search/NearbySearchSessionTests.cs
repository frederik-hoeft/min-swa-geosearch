using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Search;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class NearbySearchSessionTests
{
    [TestMethod]
    public async Task OlderSuccessAndFailureCannotReplaceNewerNearbyQueryAsync()
    {
        using DeferredReverseHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        using NearbySearchSession nearby = new(new SearchApiClient(client));
        AppliedNearbyQuery a = new(new NearbyCriteria("Apotheke", 500), new GeoCoordinate(52.3, 9.7));
        AppliedNearbyQuery b = new(new NearbyCriteria("Supermarkt", 1000), new GeoCoordinate(52.4, 9.8));

        long firstRevision = nearby.Begin();
        Task<SearchLoadState?> first = nearby.FetchAsync(a, firstRevision);
        long secondRevision = nearby.Begin();
        Task<SearchLoadState?> second = nearby.FetchAsync(b, secondRevision);
        Assert.AreEqual(2, handler.RequestCount);

        handler.Complete(1, """{"type":"FeatureCollection","snapshot":"test","features":[]}""");
        Assert.IsInstanceOfType<ReadySearch>(await second);
        handler.Complete(0, """{"type":"FeatureCollection","snapshot":"test","features":[]}""");
        Assert.IsNull(await first);
    }

    [TestMethod]
    public async Task InvalidateRejectsLateCompletionAfterChangingDiscoveryAsync()
    {
        using DeferredReverseHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        using NearbySearchSession nearby = new(new SearchApiClient(client));
        long revision = nearby.Begin();
        Task<SearchLoadState?> request = nearby.FetchAsync(new AppliedNearbyQuery(new NearbyCriteria(),
            new GeoCoordinate(52.3, 9.7)), revision);
        nearby.Invalidate();
        handler.Complete(0, """{"type":"FeatureCollection","snapshot":"test","features":[]}""");
        Assert.IsNull(await request);
    }
}
