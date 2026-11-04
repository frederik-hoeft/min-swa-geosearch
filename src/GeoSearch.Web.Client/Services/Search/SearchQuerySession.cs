namespace GeoSearch.Web.Client.Services.Search;

/// <summary>
/// Page-owned request session. The autocomplete preview and a committed literal query
/// share the same task, including when the user commits before the debounce expires.
/// Only successful completions are retained for subsequent lookups.
/// </summary>
internal sealed class SearchQuerySession(SearchApiClient apiClient) : IDisposable
{
    private const int MAX_ENTRIES = 16;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<(string Query, int Limit), Task<SearchFetchResult>> _requests = [];
    private bool _disposed;

    public Task<SearchFetchResult> FetchAsync(string query, int limit = 10)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            (string Query, int Limit) key = (query, limit);
            if (_requests.TryGetValue(key, out Task<SearchFetchResult>? existing))
            {
                // Reuse pending requests (including literal-query adoption) and successful snapshots.
                // Also handle the small window before the failure-eviction continuation runs.
                if (!existing.IsCompleted || IsSuccessful(existing))
                {
                    return existing;
                }

                _requests.Remove(key);
            }

            if (_requests.Count >= MAX_ENTRIES)
            {
                // Keep a small cache for active interactions, not an unbounded browser history.
                (string Query, int Limit) first = _requests.Keys.First();
                _requests.Remove(first);
            }

            Task<SearchFetchResult> request = apiClient.SearchAsync(query, limit, _lifetime.Token);
            _requests.Add(key, request);
            _ = request.ContinueWith(
                completed => EvictIfUnsuccessful(key, completed),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return request;
        }
    }

    public Task<SearchFetchResult> RefreshAsync(string query, int limit = 10)
    {
        lock (_gate)
        {
            _requests.Remove((query, limit));
            return FetchAsync(query, limit);
        }
    }

    private static bool IsSuccessful(Task<SearchFetchResult> completed)
    {
        if (!completed.IsCompletedSuccessfully)
        {
            return false;
        }

        // The completion check above guarantees Result cannot synchronously block.
#pragma warning disable CA1849
        return completed.Result is SearchSuccess;
#pragma warning restore CA1849
    }

    private void EvictIfUnsuccessful((string Query, int Limit) key, Task<SearchFetchResult> completed)
    {
        if (IsSuccessful(completed))
        {
            return;
        }

        // Observe an unexpected fault as well as ordinary SearchFailure/cancellation.
        _ = completed.Exception;
        lock (_gate)
        {
            // A late completion must never remove a newer request for the same query.
            if (_requests.TryGetValue(key, out Task<SearchFetchResult>? cached) && ReferenceEquals(cached, completed))
            {
                _requests.Remove(key);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
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
}
