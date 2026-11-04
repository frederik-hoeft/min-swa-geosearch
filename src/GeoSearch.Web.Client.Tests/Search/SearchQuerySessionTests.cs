using GeoSearch.Web.Client.Services.Search;
using GeoSearch.Web.Shared.GeoJson;
using System.Net;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class SearchQuerySessionTests
{
    [TestMethod]
    public async Task CommitCanAdoptInFlightPreviewWithoutDuplicateRequestAsync()
    {
        using PendingHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        using SearchQuerySession session = new(new SearchApiClient(client));

        Task<SearchFetchResult> preview = session.FetchAsync("Apotheke");
        Task<SearchFetchResult> commit = session.FetchAsync("Apotheke");

        Assert.AreSame(preview, commit);
        Assert.AreEqual(1, handler.RequestCount);
        Assert.AreEqual("/api/search?q=Apotheke&limit=10", handler.LastRequest?.RequestUri?.PathAndQuery);
        handler.Complete("""{"type":"FeatureCollection","snapshot":"test","features":[]}""");
        Assert.IsInstanceOfType<SearchSuccess>(await commit);
    }

    [TestMethod]
    public async Task SearchResponseWithPointGeometryDeserializesAsync()
    {
        using PendingHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        using SearchQuerySession session = new(new SearchApiClient(client));

        Task<SearchFetchResult> request = session.FetchAsync("Apotheke");
        handler.Complete("""
            {
              "type": "FeatureCollection",
              "snapshot": "2026-10-28",
              "attribution": "© OpenStreetMap contributors, ODbL",
              "features": [
                {
                  "type": "Feature",
                  "geometry": { "type": "Point", "coordinates": [9.71497, 52.36677] },
                  "properties": {
                    "id": "pois:123", "name": "Markt-Apotheke", "label": "Markt-Apotheke, Hannover",
                    "type": "poi", "category": "Apotheke", "score": 0.18, "distance_m": null
                  }
                }
              ]
            }
            """);

        SearchFetchResult result = await request;
        Assert.IsInstanceOfType<SearchSuccess>(result);
        GeoJsonFeature feature = ((SearchSuccess)result).Collection.Features[0];
        Assert.AreEqual("Markt-Apotheke, Hannover", feature.Properties.Label);
        Assert.AreEqual(9.71497, feature.Geometry.Coordinates[0]);
        Assert.AreEqual(52.36677, feature.Geometry.Coordinates[1]);
    }

    [TestMethod]
    public async Task ServerValidationErrorIsDisplayedAndCanBeRetriedAsync()
    {
        using PendingHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        using SearchQuerySession session = new(new SearchApiClient(client));

        Task<SearchFetchResult> first = session.FetchAsync("a&b");
        handler.Complete("""{"error":"Invalid query"}""", HttpStatusCode.BadRequest);
        SearchFetchResult failure = await first;
        Assert.IsInstanceOfType<SearchFailure>(failure);
        Assert.AreEqual("Invalid query", ((SearchFailure)failure).Message);
        Assert.AreEqual("/api/search?q=a%26b&limit=10", handler.LastRequest?.RequestUri?.PathAndQuery);

        Task<SearchFetchResult> retry = session.RefreshAsync("a&b");
        Assert.AreEqual(2, handler.RequestCount);
        handler.Complete("""{"type":"FeatureCollection","snapshot":"test","features":[]}""");
        Assert.IsInstanceOfType<SearchSuccess>(await retry);
    }

    [TestMethod]
    public async Task DisposingSessionCancelsPendingRequestAsync()
    {
        using PendingHandler handler = new();
        using HttpClient client = new(handler) { BaseAddress = new Uri("https://example.test/") };
        using SearchQuerySession session = new(new SearchApiClient(client));

        Task<SearchFetchResult> pending = session.FetchAsync("test");
        session.Dispose();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await pending);
    }
}
