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
    public async Task MixedMatchResultsRetainAnObservedTopResultRegardlessOfResponseOrderAsync()
    {
        foreach (bool emptyFirst in new[] { false, true })
        {
            int calls = 0;
            using HttpClient client = CreateClient((_, _) =>
            {
                calls++;
                bool empty = emptyFirst ? calls == 1 : calls == 5;
                return Task.FromResult(empty
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new GeoJsonFeatureCollection("2026-10-28", [])),
                    }
                    : GeoJsonResponse("poi:matched", "Lindener Rathaus"));
            });

            SearchFixtureBenchmarkService service = new(client);
            SearchFixtureResult result = await service.RunAsync(SearchFixtureCatalog.All[2], 5);

            Assert.AreEqual(5, result.SuccessfulRequests);
            Assert.AreEqual(1, result.EmptyResponses);
            Assert.AreEqual("poi:matched", result.TopResult?.Id);
            Assert.IsTrue(result.HasMixedMatches);
            Assert.IsTrue(result.TopResultVaried);
            Assert.AreEqual("Mixed matches", result.StatusLabel);

            SearchFixtureEvidence evidence = new(5, new DateTimeOffset(2026, 11, 4, 20, 0, 0, TimeSpan.FromHours(1)),
                new DateTimeOffset(2026, 11, 4, 20, 1, 0, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1), false, [result]);
            string markdown = evidence.ToMarkdown();
            StringAssert.Contains(markdown, "poi:matched", StringComparison.Ordinal);
            StringAssert.Contains(markdown, "1/5 successful responses returned no matches", StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public async Task ConsistentlyEmptyResponsesRemainNoMatchesAsync()
    {
        using HttpClient client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new GeoJsonFeatureCollection("2026-10-28", [])),
        }));
        SearchFixtureBenchmarkService service = new(client);

        SearchFixtureResult result = await service.RunAsync(SearchFixtureCatalog.All[0], 5);

        Assert.AreEqual(5, result.SuccessfulRequests);
        Assert.AreEqual(5, result.EmptyResponses);
        Assert.IsNull(result.TopResult);
        Assert.IsFalse(result.HasMixedMatches);
        Assert.IsFalse(result.TopResultVaried);
        Assert.AreEqual("No matches", result.StatusLabel);
    }

    [TestMethod]
    public void ExportUsesRecordedRunMetadataAfterSelectedSampleCountChanges()
    {
        int selectedSamples = 5;
        DateTimeOffset startedAt = new(2026, 11, 4, 20, 15, 0, TimeSpan.FromHours(1));
        DateTimeOffset completedAt = startedAt.AddSeconds(12);
        SearchFixtureResult result = new(SearchFixtureCatalog.All[0], selectedSamples, 5, [10, 11, 12, 13, 14],
            new GeoJsonProperties("addr:1", "Limmerstraße 12", "Limmerstraße 12, Hannover", "address", "Adresse", 1, null),
            "2026-10-28", 0, 0, null, false, false);
        SearchFixtureEvidence recorded = new(selectedSamples, startedAt, completedAt, TimeSpan.FromSeconds(12), false, [result]);

        // Changing the selector or copying later must not alter already-captured evidence.
        selectedSamples = 100;
        string exported = recorded.ToMarkdown();

        Assert.AreEqual(100, selectedSamples);
        StringAssert.Contains(exported, "2026-11-04 20:15:00 +01:00", StringComparison.Ordinal);
        StringAssert.Contains(exported, "2026-11-04 20:15:12 +01:00", StringComparison.Ordinal);
        StringAssert.Contains(exported, "5 requested per fixture", StringComparison.Ordinal);
        StringAssert.Contains(exported, "5/5 (target 5)", StringComparison.Ordinal);
        Assert.IsFalse(exported.Contains("100 requested", StringComparison.Ordinal));
        Assert.AreEqual(exported, recorded.ToMarkdown());
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
    public async Task OnlySupportedSampleCountsCanBeRequestedAsync()
    {
        using HttpClient client = CreateClient((_, _) => Task.FromResult(GeoJsonResponse("poi:1", "Zoo")));
        SearchFixtureBenchmarkService service = new(client);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () => await service.RunAsync(SearchFixtureCatalog.All[0], 0));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () => await service.RunAsync(SearchFixtureCatalog.All[0], 11));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () => await service.RunAsync(SearchFixtureCatalog.All[0], 26));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () => await service.RunAsync(SearchFixtureCatalog.All[0], 101));
    }

    [TestMethod]
    public async Task LargerRunsExecuteAllRequestedSamplesAsync()
    {
        int calls = 0;
        using HttpClient client = CreateClient((_, _) =>
        {
            calls++;
            return Task.FromResult(GeoJsonResponse("poi:1", "Zoo"));
        });
        SearchFixtureBenchmarkService service = new(client);

        foreach (int samples in new[] { 25, 100 })
        {
            SearchFixtureResult result = await service.RunAsync(SearchFixtureCatalog.All[8], samples);
            Assert.AreEqual(samples, result.Attempts);
            Assert.AreEqual(samples, result.SuccessfulRequests);
        }

        Assert.AreEqual(125, calls);
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
