using GeoSearch.Web.Client.Search.Model;

namespace GeoSearch.Web.Client.Services.Search;

/// <summary>
/// Guards committed-query responses against newer commits, selections, and Back.
/// The query cache is shared with autocomplete so in-flight work can be adopted.
/// </summary>
internal sealed class CommittedSearchSession(SearchQuerySession queries)
{
    private long _revision;

    public long Begin() => ++_revision;

    public void Invalidate() => ++_revision;

    public async Task<SearchLoadState?> FetchAsync(string query, int limit, long revision, bool refresh)
    {
        Task<SearchFetchResult> request = refresh
            ? queries.RefreshAsync(query, limit)
            : queries.FetchAsync(query, limit);
        SearchFetchResult result = await request;
        if (revision != _revision)
        {
            return null;
        }

        return result switch
        {
            SearchSuccess success => new ReadySearch(success.Collection),
            SearchFailure failure => new FailedSearch(failure.Message),
            _ => throw new InvalidOperationException("Unexpected search outcome.")
        };
    }
}
