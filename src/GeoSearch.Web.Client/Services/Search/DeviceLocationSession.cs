using GeoSearch.Web.Client.Search.Model;

namespace GeoSearch.Web.Client.Services.Search;

/// <summary>Explicit, cancelable browser-location acquisition with navigation-intent gating.</summary>
internal sealed class DeviceLocationSession(Func<CancellationToken, Task<DeviceLocationResult>> locate,
    Func<Task> render, Func<Task> onMapChanged, Func<GeoCoordinate, Task> inspectPoint,
    Func<long> navigationRevision) : IDisposable
{
    private CancellationTokenSource? _locationRequest;
    private bool _disposed;

    public DeviceLocationFix? Device { get; private set; }
    public string? LocationError { get; private set; }
    public bool Locating { get; private set; }

    public async Task LocateAsync()
    {
        if (_disposed || Locating)
        {
            return;
        }

        Locating = true;
        LocationError = null;
        long intent = navigationRevision();
        using CancellationTokenSource request = new();
        _locationRequest = request;
        await render();
        try
        {
            DeviceLocationResult result = await locate(request.Token);
            if (_disposed || request.IsCancellationRequested)
            {
                return;
            }
            if (result.Fix is { IsValid: true } fix)
            {
                Device = fix;
                await onMapChanged();
                if (intent == navigationRevision())
                {
                    await inspectPoint(fix.Point);
                }
            }
            else
            {
                LocationError = result.Error ?? "Device location is unavailable.";
            }
        }
        catch (OperationCanceledException) when (_disposed || request.IsCancellationRequested)
        {
            // Component disposed or location superseded.
        }
        finally
        {
            if (ReferenceEquals(_locationRequest, request))
            {
                _locationRequest = null;
            }
            Locating = false;
            if (!_disposed)
            {
                await render();
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
        _locationRequest?.Cancel();
        _locationRequest?.Dispose();
    }
}
