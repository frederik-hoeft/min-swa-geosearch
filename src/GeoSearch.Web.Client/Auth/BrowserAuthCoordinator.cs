using Microsoft.JSInterop;

namespace GeoSearch.Web.Client.Auth;

/// <summary>Serializes refresh rotations across components and browser tabs sharing a cookie.</summary>
public sealed class BrowserAuthCoordinator(IJSRuntime js) : IAsyncDisposable
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private IJSObjectReference? _module;

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await _mutex.WaitAsync(cancellationToken);
        string id = Guid.NewGuid().ToString("N");
        IJSObjectReference? module = null;
        bool acquired = false;
        try
        {
            module = _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/authCoordination.js");
            Task acquisition = module.InvokeVoidAsync("acquire", CancellationToken.None, "geosearch-browser-auth", id).AsTask();
            try
            {
                await acquisition.WaitAsync(cancellationToken);
                acquired = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await module.InvokeVoidAsync("cancelAndRelease", CancellationToken.None, id);
                await acquisition;
                throw;
            }

            return await action(cancellationToken);
        }
        finally
        {
            try
            {
                if (acquired && module is not null)
                {
                    await module.InvokeVoidAsync("release", CancellationToken.None, id);
                }
            }
            finally
            {
                _mutex.Release();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _mutex.WaitAsync();
        try
        {
            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        finally
        {
            _mutex.Release();
            _mutex.Dispose();
        }
    }
}
