using GeoSearch.Web.Client.Services.Testing;
using GeoSearch.Web.Shared.Api;
using GeoSearch.Web.Shared.GeoJson;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace GeoSearch.Web.Client.Tests.Services.Testing;

[TestClass]
public sealed class SearchFixtureBenchmarkServiceTests
{
    [TestMethod]
    public void CatalogContainsExactlyTenLabQueries()
    {
        string[] expected =
        [
            "Limmerstraße 12", "Limmerstr 12 Hannover", "Rathuas Linden", "Linden-Mitte", "Garbsen",
            "Herrenhäuser Gärten", "Lister Meile", "Hauptstrasse Laatzen", "Zoo Hannover", "Markthalle Hannover",
        ];

        CollectionAssert.AreEqual(expected, SearchFixtureCatalog.All.Select(fixture => fixture.Query).ToArray());
        Assert.AreEqual(3, SearchFixtureCatalog.All.Count(fixture => fixture.KnownRankingIssue));
    }

    [TestMethod]
    public void LatencyStatisticsHandleUnsortedEvenAndOddSamples()
    {
        SearchFixture fixture = SearchFixtureCatalog.All[0];
        SearchFixtureResult five = new(fixture, 5, 5, [30, 10, 5, 20, 15], null, "2026-10-28", 0, 0, null, false, false);
        Assert.AreEqual(5, five.BestMs);
        Assert.AreEqual(30, five.WorstMs);
        Assert.AreEqual(16, five.AverageMs);
        Assert.AreEqual(15, five.MedianMs);

        SearchFixtureResult ten = new(fixture, 10, 10, [9, 3, 5, 8, 1, 6, 10, 4, 7, 2], null, "2026-10-28", 0, 0, null, false, false);
        Assert.AreEqual(5.5, ten.MedianMs);
    }

    [TestMethod]
    public async Task FiveSamplesUseRealSearchRouteAndRecordTopResultAndLatenciesAsync()
    {
        int calls = 0;
        using HttpClient client = CreateClient((request, _) =>
        {
            calls++;
            Assert.AreEqual(HttpMethod.Get, request.Method);
            StringAssert.Contains(request.RequestUri!.Query, "q=Limmerstr%2012%20Hannover", StringComparison.Ordinal);
            StringAssert.Contains(request.RequestUri.Query, "limit=1", StringComparison.Ordinal);
            return Task.FromResult(GeoJsonResponse("address:1", "Limmerstraße 12"));
        });

        SearchFixtureBenchmarkService service = new(client);
        SearchFixtureResult result = await service.RunAsync(SearchFixtureCatalog.All[1], 5);

        Assert.AreEqual(5, calls);
        Assert.AreEqual(5, result.Attempts);
        Assert.AreEqual(5, result.SuccessfulRequests);
        Assert.AreEqual(0, result.FailedRequests);
        Assert.AreEqual("address:1", result.TopResult?.Id);
        Assert.AreEqual("2026-10-28", result.Snapshot);
        Assert.IsNotNull(result.BestMs);
        Assert.IsTrue(result.BestMs >= 0);
        Assert.IsTrue(result.BestMs <= result.AverageMs);
        Assert.IsTrue(result.AverageMs <= result.WorstMs);
        Assert.IsTrue(result.MedianMs >= result.BestMs && result.MedianMs <= result.WorstMs);
        Assert.IsFalse(result.TopResultVaried);
        Assert.IsFalse(result.SnapshotVaried);
    }

    [TestMethod]
    public async Task TenSamplesDetectResultAndSnapshotChangesAsync()
    {
        int calls = 0;
        using HttpClient client = CreateClient((_, _) =>
        {
            calls++;
            string id = calls <= 5 ? "poi:first" : "poi:second";
            string snapshot = calls <= 5 ? "2026-10-28" : "2026-11-04";
            return Task.FromResult(GeoJsonResponse(id, "Zoo", snapshot));
        });

        SearchFixtureBenchmarkService service = new(client);
        SearchFixtureResult result = await service.RunAsync(SearchFixtureCatalog.All[8], 10);

        Assert.AreEqual(10, result.Attempts);
        Assert.AreEqual(10, result.SuccessfulRequests);
        Assert.IsTrue(result.TopResultVaried);
        Assert.IsTrue(result.SnapshotVaried);
        Assert.AreEqual("poi:second", result.TopResult?.Id);
    }

    [TestMethod]
    public async Task HttpErrorsAndEmptyResultsDoNotInventTopResultsAsync()
    {
        int calls = 0;
        using HttpClient client = CreateClient((_, _) =>
        {
            calls++;
            return Task.FromResult(calls % 2 == 0
                ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new ApiError("Invalid query")) }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new GeoJsonFeatureCollection("2026-10-28", [])),
                });
        });

        SearchFixtureBenchmarkService service = new(client);
        SearchFixtureResult result = await service.RunAsync(SearchFixtureCatalog.All[0], 5);

        Assert.AreEqual(5, result.Attempts);
        Assert.AreEqual(3, result.SuccessfulRequests);
        Assert.AreEqual(3, result.EmptyResponses);
        Assert.AreEqual(2, result.FailedRequests);
        Assert.IsNull(result.TopResult);
        StringAssert.Contains(result.LastError!, "HTTP 400: Invalid query", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task InvalidGeoJsonIsAnErrorNotSuccessfulLatencyAsync()
    {
        using HttpClient client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{invalid json", Encoding.UTF8, "application/json"),
        }));

        SearchFixtureBenchmarkService service = new(client);
        SearchFixtureResult result = await service.RunAsync(SearchFixtureCatalog.All[0], 5);

        Assert.AreEqual(0, result.SuccessfulRequests);
        Assert.AreEqual(5, result.FailedRequests);
        Assert.AreEqual("The API returned an invalid GeoJSON response.", result.LastError);
    }

    [TestMethod]
    public async Task NetworkFailureStopsRepeatingAnUnreachableEndpointAsync()
    {
        using HttpClient client = CreateClient((_, _) => throw new HttpRequestException("Offline"));
        SearchFixtureBenchmarkService service = new(client);

        SearchFixtureResult result = await service.RunAsync(SearchFixtureCatalog.All[0], 10);

        Assert.AreEqual(1, result.Attempts);
        Assert.AreEqual(1, result.FailedRequests);
        StringAssert.Contains(result.LastError!, "Offline", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task CallerCancellationIsPropagatedAsync()
    {
        using CancellationTokenSource cancellation = new();
        using HttpClient client = CreateClient(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return GeoJsonResponse("poi:1", "Zoo");
        });

        SearchFixtureBenchmarkService service = new(client);
        Task<SearchFixtureResult> run = service.RunAsync(SearchFixtureCatalog.All[0], 5, cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await run);
    }

    [TestMethod]
    public async Task OnlyFiveOrTenSamplesCanBeRequestedAsync()
    {
        using HttpClient client = CreateClient((_, _) => Task.FromResult(GeoJsonResponse("poi:1", "Zoo")));
        SearchFixtureBenchmarkService service = new(client);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () => await service.RunAsync(SearchFixtureCatalog.All[0], 0));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () => await service.RunAsync(SearchFixtureCatalog.All[0], 11));
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        // HttpClient takes ownership of the handler (disposeHandler: true).
#pragma warning disable CA2000 // Ownership transfer is explicit, but the analyzer cannot trace it.
        SearchHandler handler = new(respond);
#pragma warning restore CA2000
        try
        {
            return new HttpClient(handler, disposeHandler: true) { BaseAddress = new Uri("https://localhost/") };
        }
        catch
        {
            handler.Dispose();
            throw;
        }
    }

    private static HttpResponseMessage GeoJsonResponse(string id, string name, string snapshot = "2026-10-28") =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new GeoJsonFeatureCollection(snapshot,
            [
                new GeoJsonFeature(new GeoJsonPoint(9.73, 52.37),
                    new GeoJsonProperties(id, name, $"{name}, Hannover", "poi", "Test", 0.9f, null)),
            ])),
        };

    private sealed class SearchHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }
}
