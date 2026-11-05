using GeoSearch.Web.Client.Search.Model;

namespace GeoSearch.Web.Client.Services.Search;

/// <summary>
/// Owns cancellation and generation tracking for map-point reverse requests.
/// Neither late successes nor late failures can outlive an inspection transition.
/// </summary>
internal sealed class ReverseLookupSession(SearchApiClient api) : IDisposable
{
    private CancellationTokenSource? _request;
    private long _revision;
    private bool _disposed;

    public long Begin()
    {
        Invalidate();
        return _revision;
    }

    public bool IsCurrent(long revision) => !_disposed && revision == _revision;

    public void Invalidate()
    {
        ++_revision;
        CancellationTokenSource? request = _request;
        _request = null;
        // The owning FetchAsync disposes its CTS on completion, including when
        // an HTTP handler ignores cancellation and completes later.
        request?.Cancel();
    }

    public async Task<SearchLoadState?> FetchAsync(GeoCoordinate point, long revision)
    {
        if (!IsCurrent(revision))
        {
            return null;
        }

        using CancellationTokenSource request = new();
        _request = request;
        try
        {
            SearchFetchResult result = await api.ReverseAsync(point, request.Token);
            if (!IsCurrent(revision) || request.IsCancellationRequested)
            {
                return null;
            }

            return result switch
            {
                SearchSuccess success => new ReadySearch(success.Collection),
                SearchFailure failure => new FailedSearch(failure.Message),
                _ => throw new InvalidOperationException("Unexpected reverse lookup outcome.")
            };
        }
        catch (OperationCanceledException) when (_disposed || request.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            if (ReferenceEquals(_request, request))
            {
                _request = null;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancellationTokenSource? pending = _request;
        Invalidate();
        pending?.Dispose();
    }
}
