using GeoSearch.Web.Client.Search.Model;

namespace GeoSearch.Web.Client.Search;

/// <summary>
/// Confirms single-click map inspection without intercepting MapLibre's native
/// double-click zoom. A newer click supersedes a pending candidate, and a
/// double-click cancels it outright.
/// </summary>
internal sealed class BackgroundMapClickCoordinator(
    Func<GeoCoordinate, Task> onConfirmedClick,
    Func<CancellationToken, Task>? delay = null) : IDisposable
{
    private static readonly TimeSpan s_doubleClickWindow = TimeSpan.FromMilliseconds(450);
    private readonly Lock _gate = new();
    private readonly Func<CancellationToken, Task> _delay = delay ??
        (cancellationToken => Task.Delay(s_doubleClickWindow, cancellationToken));
    private CancellationTokenSource? _pending;
    private bool _disposed;

    public async Task ScheduleAsync(GeoCoordinate point)
    {
        if (!point.IsValid)
        {
            return;
        }

        using CancellationTokenSource candidate = new();
        CancellationToken token = candidate.Token;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            CancelPendingCore();
            _pending = candidate;
        }

        try
        {
            await _delay(token);
            lock (_gate)
            {
                if (_disposed || !ReferenceEquals(_pending, candidate) || candidate.IsCancellationRequested)
                {
                    return;
                }

                _pending = null;
            }

            await onConfirmedClick(point);
        }
        catch (OperationCanceledException) when (candidate.IsCancellationRequested)
        {
            // A second click, double-click, draft edit, or disposal superseded it.
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_pending, candidate))
                {
                    _pending = null;
                }
            }
        }
    }

    public void CancelPending()
    {
        lock (_gate)
        {
            CancelPendingCore();
        }
    }

    private void CancelPendingCore()
    {
        // Synchronous cancellation is intentional: the click must be consumed
        // before a following dblclick or UI action can be processed.
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _pending?.Cancel();
            _pending?.Dispose();
            _pending = null;
        }
    }
}
