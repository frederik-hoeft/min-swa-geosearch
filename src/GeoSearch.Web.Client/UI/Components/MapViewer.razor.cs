using Community.Blazor.MapLibre;
using Community.Blazor.MapLibre.Models;
using Community.Blazor.MapLibre.Models.Control;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http;
using System.Text.Json;

namespace GeoSearch.Web.Client.UI.Components;

public partial class MapViewer : ComponentBase, IAsyncDisposable
{
    private const string DEFAULT_STYLE_URL = "map-styles/osm-standard.json";
    private static readonly TimeSpan s_styleCheckTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_mapLoadTimeout = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _lifetime = new();
    private MapLibre? _map;
    private IJSObjectReference? _browserModule;
    private MapOptions _options = new();
    private bool _styleChecked;
    private bool _browserProbeStarted;
    private bool _browserChecked;
    private bool _supportsWebGl2;
    private bool _mapLoaded;
    private bool _isErrorListenerRegistered;
    private bool _hasTileError;
    private bool _mapLoadTimedOut;
    private bool _disposed;
    private string? _startupError;

    [Inject]
    private IConfiguration Configuration { get; set; } = null!;

    [Inject]
    private HttpClient Http { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private ILogger<MapViewer> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    protected async override Task OnInitializedAsync()
    {
        string styleUrl = Configuration["Map:StyleUrl"] ?? DEFAULT_STYLE_URL;
        _options = new MapOptions
        {
            Style = styleUrl,
            Center = new LngLat(longitude: 9.7320, latitude: 52.3759),
            Zoom = 11.5,
            MaxZoom = 19,
            AttributionControl = false,
            TrackResize = true,
            DragRotate = false
        };

        // The wrapper exposes no error callback until style.load. Check the style
        // before mounting MapLibre, so a missing/malformed style cannot hang silently.
        // Preserve the URL as MapLibre's style source for relative sprite/glyph paths.
        try
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            timeout.CancelAfter(s_styleCheckTimeout);
            Uri styleUri = new(new Uri(Navigation.BaseUri), styleUrl);
            if (styleUri.Scheme is not ("http" or "https"))
            {
                throw new UriFormatException("The map style must use HTTP or HTTPS.");
            }

            using HttpResponseMessage response = await Http.GetAsync(styleUri, timeout.Token);
            response.EnsureSuccessStatusCode();
            await using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using JsonDocument style = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            JsonElement root = style.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("version", out JsonElement version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int styleVersion) || styleVersion != 8 ||
                !root.TryGetProperty("sources", out JsonElement sources) || sources.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("layers", out JsonElement layers) || layers.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("The configured map style is not a MapLibre style version 8 object.");
            }

            _styleChecked = true;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Navigated away while loading; do not show an error.
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or UriFormatException or OperationCanceledException)
        {
            LogMapStyleFailure(Logger, exception, styleUrl);
            _startupError = "The map style could not be loaded. Check the configured style URL and your connection.";
        }
    }

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        // OnInitializedAsync may have rendered the preparing state before the
        // HTTP style check completed, so firstRender is not a reliable gate.
        if (!_styleChecked || _browserProbeStarted || _disposed)
        {
            return;
        }

        _browserProbeStarted = true;
        try
        {
            _browserModule = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./UI/Components/MapViewer.razor.js");
            _supportsWebGl2 = await _browserModule.InvokeAsync<bool>("supportsWebGl2");
            _browserChecked = true;
            await InvokeAsync(StateHasChanged);
            if (_supportsWebGl2)
            {
                _ = WatchMapLoadAsync();
            }
        }
        catch (Exception exception)
        {
            if (_disposed)
            {
                return;
            }

            LogMapBrowserFailure(Logger, exception);
            _startupError = "The map could not start. Check your browser settings and reload the page.";
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task WatchMapLoadAsync()
    {
        try
        {
            await Task.Delay(s_mapLoadTimeout, _lifetime.Token);
            if (!_mapLoaded && !_hasTileError && _startupError is null && !_disposed)
            {
                await InvokeAsync(() =>
                {
                    _mapLoadTimedOut = true;
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Normal component disposal.
        }
    }

    private async Task OnStyleLoadedAsync(EventArgs _)
    {
        if (_map is null || _isErrorListenerRegistered || _disposed)
        {
            return;
        }

        try
        {
            // Observe subsequent tile/source failures. Errors before style.load are
            // covered by the preflight check and bounded load watchdog above.
            await _map.AddAsyncListener<JsonElement>("error", OnMapErrorAsync);
            _isErrorListenerRegistered = true;
        }
        catch (JSException exception)
        {
            LogMapErrorListenerFailure(Logger, exception);
            _startupError = "The map could not finish initializing. Reload the page to try again.";
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OnMapLoadedAsync(EventArgs _)
    {
        if (_map is null || _startupError is not null || _disposed)
        {
            return;
        }

        // Add attribution first so it sits above navigation controls in the top-right stack.
        await _map.AddControl(ControlType.AttributionControl, ControlPosition.TopRight, new { compact = false });
        await _map.AddControl(ControlType.NavigationControl, ControlPosition.TopRight, new { showCompass = false });
        _mapLoaded = true;
        _mapLoadTimedOut = false;
        await InvokeAsync(StateHasChanged);
    }

    private Task OnMapErrorAsync(JsonElement _)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        _hasTileError = true;
        return InvokeAsync(StateHasChanged);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Could not load or validate map style at {StyleUrl}")]
    private static partial void LogMapStyleFailure(ILogger logger, Exception exception, string styleUrl);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "Could not initialize the map browser module")]
    private static partial void LogMapBrowserFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "Could not register map error listener")]
    private static partial void LogMapErrorListenerFailure(ILogger logger, Exception exception);

    private void DismissError() => _hasTileError = false;

    private void Reload() => Navigation.NavigateTo(Navigation.Uri, forceLoad: true);

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await _lifetime.CancelAsync();
        _lifetime.Dispose();
        if (_browserModule is not null)
        {
            try
            {
                await _browserModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Browser already disconnected during navigation or shutdown.
            }
        }

        GC.SuppressFinalize(this);
    }
}
