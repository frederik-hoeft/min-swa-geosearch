using GeoSearch.Web.Shared.Api;
using GeoSearch.Web.Shared.GeoJson;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeoSearch.Web.Client.Services.Testing;

/// <summary>Measures sequential, full-body HTTP search responses without involving the map UI.</summary>
public sealed class SearchFixtureBenchmarkService(HttpClient httpClient)
{
    private static readonly TimeSpan s_requestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Runs a fixture through the actual search endpoint. Timings include the HTTP response body
    /// download but exclude JSON parsing; failed/invalid responses are not included in latency statistics.
    /// </summary>
    public async Task<SearchFixtureResult> RunAsync(SearchFixture fixture, int samples, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        if (samples is not (5 or 10 or 25 or 100))
        {
            throw new ArgumentOutOfRangeException(nameof(samples), samples, "Use 5, 10, 25, or 100 samples per query.");
        }

        return await MeasureAsync(fixture, samples, cancellationToken);
    }

    private async Task<SearchFixtureResult> MeasureAsync(SearchFixture fixture, int samples, CancellationToken cancellationToken)
    {
        List<double> latencies = new(samples);
        GeoJsonProperties? topResult = null;
        string? snapshot = null;
        string? initialResultId = null;
        string? initialSnapshot = null;
        string? lastError = null;
        int attempted = 0;
        int emptyResponses = 0;
        int failedRequests = 0;
        bool hasInitialResult = false;
        bool resultVaried = false;
        bool snapshotVaried = false;
        Uri requestUri = new($"api/search?q={Uri.EscapeDataString(fixture.Query)}&limit=1", UriKind.Relative);

        for (int i = 0; i < samples; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempted++;
            using CancellationTokenSource requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestTimeout.CancelAfter(s_requestTimeout);
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                // GetAsync buffers the complete response body by default, not just the headers.
                using HttpResponseMessage response = await httpClient.GetAsync(requestUri, requestTimeout.Token);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    failedRequests++;
                    lastError = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
                    try
                    {
                        ApiError? error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: requestTimeout.Token);
                        if (!string.IsNullOrWhiteSpace(error?.Error))
                        {
                            lastError = $"HTTP {(int)response.StatusCode}: {error.Error}";
                        }
                    }
                    catch (JsonException)
                    {
                        // An error page or empty body may not use the API's JSON error contract.
                    }
                    catch (NotSupportedException)
                    {
                        // Retain the HTTP status even if error-body deserialization is unsupported.
                    }

                    continue;
                }

                SearchResponse collection = await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken: requestTimeout.Token)
                    ?? throw new JsonException("The search response was empty.");
                if (collection.Features is null || string.IsNullOrWhiteSpace(collection.Snapshot))
                {
                    throw new JsonException("Missing GeoJSON features or snapshot.");
                }

                GeoJsonProperties? currentTop = collection.Features.Count > 0 ? collection.Features[0].Properties : null;
                if (collection.Features.Count > 0 && currentTop is null)
                {
                    throw new JsonException("Missing GeoJSON feature properties.");
                }

                latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
                if (currentTop is null)
                {
                    emptyResponses++;
                }

                if (hasInitialResult)
                {
                    resultVaried |= !string.Equals(initialResultId, currentTop?.Id, StringComparison.Ordinal);
                    snapshotVaried |= !string.Equals(initialSnapshot, collection.Snapshot, StringComparison.Ordinal);
                }
                else
                {
                    initialResultId = currentTop?.Id;
                    initialSnapshot = collection.Snapshot;
                    hasInitialResult = true;
                }

                topResult = currentTop;
                snapshot = collection.Snapshot;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failedRequests++;
                lastError = "The request timed out after 10 seconds.";
                break;
            }
            catch (HttpRequestException exception)
            {
                failedRequests++;
                lastError = $"Network error: {exception.Message}";
                // An unreachable API is unlikely to recover during the same fixture run.
                break;
            }
            catch (JsonException)
            {
                failedRequests++;
                lastError = "The API returned an invalid GeoJSON response.";
            }
            catch (NotSupportedException)
            {
                failedRequests++;
                lastError = "The API returned an unsupported GeoJSON response.";
            }
        }

        return new SearchFixtureResult(fixture, samples, attempted, latencies.ToArray(), topResult,
            snapshot, emptyResponses, failedRequests, lastError, resultVaried, snapshotVaried);
    }

    // This runner reads only the fields it needs. The shared GeoJsonPoint is an output-only
    // contract (longitude/latitude constructor) and cannot be deserialized directly.
    private sealed record SearchResponse(
        [property: JsonPropertyName("snapshot")] string Snapshot,
        [property: JsonPropertyName("features")] IReadOnlyList<SearchFeature> Features);

    private sealed record SearchFeature([property: JsonPropertyName("properties")] GeoJsonProperties Properties);
}
