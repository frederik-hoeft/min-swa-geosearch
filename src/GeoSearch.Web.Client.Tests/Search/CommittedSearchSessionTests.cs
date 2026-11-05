using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Search;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class CommittedSearchSessionTests
{
    [TestMethod]
    public async Task OutOfOrderCompletionsOnlyPublishLatestCommitAsync()
    {
        using DeferredReverseHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        using SearchQuerySession queries = new(new SearchApiClient(client));
        CommittedSearchSession committed = new(queries);

        long firstRevision = committed.Begin();
        Task<SearchLoadState?> first = committed.FetchAsync("first", 10, firstRevision, refresh: false);
        long secondRevision = committed.Begin();
        Task<SearchLoadState?> second = committed.FetchAsync("second", 10, secondRevision, refresh: false);
        Assert.AreEqual(2, handler.RequestCount);

        handler.Complete(1, """{"type":"FeatureCollection","snapshot":"test","features":[]}""");
        Assert.IsInstanceOfType<ReadySearch>(await second);
        handler.Complete(0, """{"type":"FeatureCollection","snapshot":"test","features":[]}""");
        Assert.IsNull(await first);
    }

    [TestMethod]
    public async Task InvalidatingSelectionRejectsInFlightQueryAsync()
    {
        using DeferredReverseHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        using SearchQuerySession queries = new(new SearchApiClient(client));
        CommittedSearchSession committed = new(queries);

        long revision = committed.Begin();
        Task<SearchLoadState?> pending = committed.FetchAsync("first", 10, revision, refresh: false);
        committed.Invalidate();
        handler.Complete(0, """{"type":"FeatureCollection","snapshot":"test","features":[]}""");
        Assert.IsNull(await pending);
    }
}
