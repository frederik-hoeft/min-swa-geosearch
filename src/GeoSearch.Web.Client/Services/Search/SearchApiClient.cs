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
        try
        {
            Uri uri = SimpleUriBuilder.Create("api")
                .AppendPath("search")
                .AppendQuery("q", query)
                .AppendQuery("limit", limit)
                .BuildUri(UriKind.Relative);
            using HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiError? error = null;
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
                }

                return new SearchFailure(error?.Error ?? $"Search failed (HTTP {(int)response.StatusCode}).");
            }

            GeoJsonFeatureCollection? collection = await response.Content.ReadFromJsonAsync<GeoJsonFeatureCollection>(cancellationToken);
            return collection is null
                ? new SearchFailure("Search returned an empty response.")
                : new SearchSuccess(collection);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new SearchFailure("Search timed out. Please retry.");
        }
        catch (HttpRequestException)
        {
            return new SearchFailure("Could not reach the search service. Check your connection and retry.");
        }
        catch (JsonException)
        {
            return new SearchFailure("Search returned an invalid response.");
        }
        catch (NotSupportedException)
        {
            return new SearchFailure("Search returned an unsupported response.");
        }
    }

    // F5 consumes this catalog. F2 does not expose category suggestions without a nearby editor.
    public Task<IReadOnlyList<string>?> GetCategoriesAsync(CancellationToken cancellationToken) =>
        httpClient.GetFromJsonAsync<IReadOnlyList<string>>(SimpleUriBuilder.Create("api").AppendPath("categories").BuildUri(UriKind.Relative), cancellationToken);
}
