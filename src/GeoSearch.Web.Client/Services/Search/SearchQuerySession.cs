namespace GeoSearch.Web.Client.Services.Search;

/// <summary>
/// Page-owned request session. The autocomplete preview and a committed literal query
/// share the same task, including when the user commits before the debounce expires.
/// </summary>
internal sealed class SearchQuerySession(SearchApiClient apiClient) : IDisposable
{
    private const int MAX_ENTRIES = 16;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<(string Query, int Limit), Task<SearchFetchResult>> _requests = [];
    private bool _disposed;

    public Task<SearchFetchResult> FetchAsync(string query, int limit = 10)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        (string Query, int Limit) key = (query, limit);
        if (_requests.TryGetValue(key, out Task<SearchFetchResult>? existing))
        {
            return existing;
        }

        if (_requests.Count >= MAX_ENTRIES)
        {
            // Keep a small cache for active interactions, not an unbounded browser history.
            (string Query, int Limit) first = _requests.Keys.First();
            _requests.Remove(first);
        }

        Task<SearchFetchResult> request = apiClient.SearchAsync(query, limit, _lifetime.Token);
        _requests.Add(key, request);
        return request;
    }

    public Task<SearchFetchResult> RefreshAsync(string query, int limit = 10)
    {
        _requests.Remove((query, limit));
        return FetchAsync(query, limit);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        _requests.Clear();
    }
}
