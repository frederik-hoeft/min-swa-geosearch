using GeoSearch.Web.Client.Search.Model;

namespace GeoSearch.Web.Client.Services.Search;

/// <summary>Owns the current nearby HTTP request and rejects superseded completions.</summary>
internal sealed class NearbySearchSession(SearchApiClient api) : IDisposable
{
    private CancellationTokenSource? _request;
    private long _revision;
    private bool _disposed;

    public long Begin()
    {
        Invalidate();
        return _revision;
    }

    public void Invalidate()
    {
        ++_revision;
        _request?.Cancel();
        _request = null;
    }

    public async Task<SearchLoadState?> FetchAsync(AppliedNearbyQuery query, long revision)
    {
        if (_disposed || revision != _revision)
        {
            return null;
        }

        using CancellationTokenSource request = new();
        _request = request;
        try
        {
            SearchFetchResult result = await api.NearbyAsync(query, request.Token);
            if (_disposed || revision != _revision || request.IsCancellationRequested)
            {
                return null;
            }

            return result switch
            {
                SearchSuccess success => new ReadySearch(success.Collection),
                SearchFailure failure => new FailedSearch(failure.Message),
                _ => throw new InvalidOperationException("Unexpected nearby outcome.")
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
