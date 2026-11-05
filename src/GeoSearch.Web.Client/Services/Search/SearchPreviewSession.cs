using GeoSearch.Web.Client.Search;
using GeoSearch.Web.Client.Search.Model;

namespace GeoSearch.Web.Client.Services.Search;

/// <summary>
/// Owns one autocomplete draft's debounce and preview snapshot. A committed
/// search can adopt the same in-flight request through the shared query session.
/// </summary>
internal sealed class SearchPreviewSession(
    SearchQuerySession queries, Func<CancellationToken, Task>? delay = null) : IDisposable
{
    private readonly Func<CancellationToken, Task> _delay = delay ??
        (token => Task.Delay(SearchQueryPolicy.DEBOUNCE_MILLISECONDS, token));
    private SearchFetchResult? _result;
    private string? _query;
    private bool _loading;
    private long _revision;
    private CancellationTokenSource? _debounce;
    private bool _disposed;

    public IReadOnlyList<SearchSuggestion> GetSuggestions(string input)
    {
        string query = input.Trim();
        if (!SearchQueryPolicy.IsValid(query))
        {
            return [];
        }

        List<SearchSuggestion> suggestions = [new LiteralQuerySuggestion(query)];
        if (_query == query && _result is SearchSuccess success)
        {
            suggestions.AddRange(success.Collection.Features.Take(3).Select(feature => new PlaceSuggestion(feature)));
        }

        return suggestions;
    }

    public string? GetInputHint(string input)
    {
        string query = input.Trim();
        if (query.Length < SearchQueryPolicy.MINIMUM_LENGTH)
        {
            return $"Enter at least {SearchQueryPolicy.MINIMUM_LENGTH} characters to search.";
        }
        if (query.Length > SearchQueryPolicy.MAXIMUM_LENGTH)
        {
            return $"Search text must be at most {SearchQueryPolicy.MAXIMUM_LENGTH} characters.";
        }
        if (_query == query)
        {
            return _result switch
            {
                SearchFailure failure => failure.Message,
                SearchSuccess { Collection.Features.Count: 0 } => "No matching places. You can still search the literal text.",
                _ => null
            };
        }

        return _loading ? "Finding suggestions..." : null;
    }

    public SearchSuccess? GetSuccessfulPreview(string query) =>
        _query == query ? _result as SearchSuccess : null;

    public void Edit(string text, Func<Task> onUpdated)
    {
        CancelPending();
        _result = null;
        _query = null;
        string query = text.Trim();
        _loading = SearchQueryPolicy.IsValid(query);
        if (_loading)
        {
            CancellationTokenSource debounce = new();
            _debounce = debounce;
            long revision = _revision;
            _ = LoadAfterDebounceAsync(query, revision, debounce.Token, onUpdated);
        }
    }

    /// <summary>Invalidate an unfinished preview without discarding a reusable completed snapshot.</summary>
    public void CancelPending()
    {
        ++_revision;
        _loading = false;
        CancellationTokenSource? debounce = _debounce;
        _debounce = null;
        if (debounce is not null)
        {
            debounce.Cancel();
            debounce.Dispose();
        }
    }

    public void Clear()
    {
        CancelPending();
        _result = null;
        _query = null;
    }

    private async Task LoadAfterDebounceAsync(string query, long revision, CancellationToken token, Func<Task> onUpdated)
    {
        try
        {
            await _delay(token);
            if (_disposed || token.IsCancellationRequested)
            {
                return;
            }

            SearchFetchResult result = await queries.FetchAsync(query);
            if (_disposed || revision != _revision)
            {
                return;
            }

            _query = query;
            _result = result;
            _loading = false;
            await onUpdated();
        }
        catch (OperationCanceledException) when (_disposed || token.IsCancellationRequested)
        {
            // Superseded by a new draft or page disposal.
        }
        catch (ObjectDisposedException) when (_disposed)
        {
            // The query session was disposed while this preview was pending.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelPending();
    }
}
