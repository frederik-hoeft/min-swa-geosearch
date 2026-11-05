using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Shared.Api;
using GeoSearch.Web.Shared.GeoJson;
using GeoSearch.Web.Shared.Web;
using System.Net.Http.Json;
using System.Text.Json;

namespace GeoSearch.Web.Client.Services.Search;

/// <summary>The browser-facing, read-only HTTP boundary for the GeoSearch API.</summary>
public sealed class SearchApiClient(HttpClient httpClient)
{
    internal async Task<SearchFetchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        Uri uri = SimpleUriBuilder.Create("api")
            .AppendPath("search")
            .AppendQuery("q", query)
            .AppendQuery("limit", limit)
            .BuildUri(UriKind.Relative);
        return await ReadCollectionAsync(uri, "Search", cancellationToken);
    }

    /// <summary>Query distance-ranked nearby POIs from a captured WGS84 origin.</summary>
    internal Task<SearchFetchResult> NearbyAsync(AppliedNearbyQuery query, CancellationToken cancellationToken)
    {
        if (!query.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(query));
        }

        SimpleUriBuilder uri = SimpleUriBuilder.Create("api")
            .AppendPath("nearby")
            .AppendQuery("lat", query.Origin.Latitude)
            .AppendQuery("lon", query.Origin.Longitude)
            .AppendQuery("radius", query.Criteria.RadiusMeters)
            .AppendQuery("limit", 20);
        if (query.Criteria.Category is { } category)
        {
            uri.AppendQuery("category", category);
        }

        return ReadCollectionAsync(uri.BuildUri(UriKind.Relative), "Nearby search", cancellationToken);
    }

    /// <summary>Inspect the nearest feature to a user-clicked WGS84 coordinate.</summary>
    internal Task<SearchFetchResult> ReverseAsync(GeoCoordinate point, CancellationToken cancellationToken)
    {
        if (!point.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(point));
        }
        Uri uri = SimpleUriBuilder.Create("api")
            .AppendPath("reverse")
            .AppendQuery("lat", point.Latitude)
            .AppendQuery("lon", point.Longitude)
            .AppendQuery("radius", 250)
            .BuildUri(UriKind.Relative);
        return ReadCollectionAsync(uri, "Reverse lookup", cancellationToken);
    }

    private async Task<SearchFetchResult> ReadCollectionAsync(Uri uri, string operation, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiError? error = null;
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
                }

                return new SearchFailure(error?.Error ?? $"{operation} failed (HTTP {(int)response.StatusCode}).");
            }

            GeoJsonFeatureCollection? collection = await response.Content.ReadFromJsonAsync<GeoJsonFeatureCollection>(cancellationToken);
            return collection is null
                ? new SearchFailure($"{operation} returned an empty response.")
                : new SearchSuccess(collection);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new SearchFailure($"{operation} timed out. Please retry.");
        }
        catch (HttpRequestException)
        {
            return new SearchFailure($"Could not reach the {operation} endpoint. Check your connection and retry.");
        }
        catch (JsonException)
        {
            return new SearchFailure($"{operation} returned an invalid response.");
        }
        catch (NotSupportedException)
        {
            return new SearchFailure($"{operation} returned an unsupported response.");
        }
    }

    // F5 consumes this catalog. F2 does not expose category suggestions without a nearby editor.
    public Task<IReadOnlyList<string>?> GetCategoriesAsync(CancellationToken cancellationToken) =>
        httpClient.GetFromJsonAsync<IReadOnlyList<string>>(SimpleUriBuilder.Create("api").AppendPath("categories").BuildUri(UriKind.Relative), cancellationToken);
}
