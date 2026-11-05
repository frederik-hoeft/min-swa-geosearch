using Community.Blazor.MapLibre;
using Community.Blazor.MapLibre.Models;
using Community.Blazor.MapLibre.Models.Control;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Theming;
using GeoSearch.Web.Client.UI.Components.Map;
using GeoSearch.Web.Shared.GeoJson;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
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
    private MapResultOverlay? _resultOverlay;
    private bool _resultOverlayReady;
    private bool _hasOverlayError;
    private bool _styleChecked;
    private bool _browserProbeStarted;
    private bool _browserChecked;
    private bool _supportsWebGl2;
    private bool _mapLoaded;
    private bool _mapLoadTimedOut;
    private bool _disposed;
    private string? _startupError;

    [Parameter]
    public SearchMapState SearchState { get; set; } = SearchMapState.Empty;

    [Parameter]
    public bool SearchPanelExpanded { get; set; }

    [Parameter]
    public EventCallback<GeoJsonFeature> OnFeatureSelected { get; set; }

    [Parameter]
    public EventCallback<GeoCoordinate> OnBackgroundClicked { get; set; }

    [Parameter]
    public EventCallback OnBackgroundDoubleClicked { get; set; }

    [Parameter]
    public EventCallback<GeoCoordinate> OnMapDragged { get; set; }

    [Inject]
    private IConfiguration Configuration { get; set; } = null!;

    [Inject]
    private IBrowserViewportService Viewport { get; set; } = null!;

    [Inject]
    private HttpClient Http { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private ThemePreferenceService ThemePreferences { get; set; } = null!;

    [Inject]
    private ILogger<MapViewer> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    protected async override Task OnInitializedAsync()
    {
        ThemePreferences.Changed += OnThemeChanged;
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

    protected async override Task OnParametersSetAsync()
    {
        if (_resultOverlayReady && _resultOverlay is not null && !_disposed)
        {
            await UpdateOverlayAsync();
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
        IJSObjectReference? module = null;
        try
        {
            // Own the imported module locally until the probe completes. Navigation
            // can dispose the component while either JS interop call is pending.
            module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./UI/Components/MapViewer.razor.js");
            if (_disposed)
            {
                return;
            }

            bool supportsWebGl2 = await module.InvokeAsync<bool>("supportsWebGl2");
            if (_disposed)
            {
                return;
            }

            // No await separates the disposal check from the ownership transfer.
            _browserModule = module;
            module = null;
            _supportsWebGl2 = supportsWebGl2;
            _browserChecked = true;
            await InvokeAsync(StateHasChanged);
            if (_supportsWebGl2 && !_disposed)
            {
                _ = WatchMapLoadAsync();
            }
        }
        catch (JSDisconnectedException) when (_disposed)
        {
            // The browser disconnected while initialization was in flight.
        }
        catch (Exception exception)
        {
            if (!_disposed)
            {
                LogMapBrowserFailure(Logger, exception);
                _startupError = "The map could not start. Check your browser settings and reload the page.";
                await InvokeAsync(StateHasChanged);
            }
        }
        finally
        {
            if (module is not null)
            {
                try
                {
                    await module.DisposeAsync();
                }
                catch (JSDisconnectedException)
                {
                    // The browser has already released the module.
                }
            }
        }
    }

    private async Task WatchMapLoadAsync()
    {
        try
        {
            await Task.Delay(s_mapLoadTimeout, _lifetime.Token);
            if (!_mapLoaded && _startupError is null && !_disposed)
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

    private async Task OnMapLoadedAsync(EventArgs _)
    {
        if (_map is null || _startupError is not null || _disposed)
        {
            return;
        }

        // Add attribution first so it sits above navigation controls in the top-right stack.
        await _map.AddControl(ControlType.AttributionControl, ControlPosition.TopRight, new { compact = false });
        await _map.AddControl(ControlType.NavigationControl, ControlPosition.TopRight, new { showCompass = false });
        try
        {
            _resultOverlay = new MapResultOverlay(
                _map, SelectKnownMapFeatureAsync, GetSelectionOffsetPixelsAsync);
            _resultOverlayReady = true;
            await UpdateOverlayAsync();
            // Marker buttons stop propagation in the wrapper. Background double
            // clicks must cancel inspection candidates without disabling zoom.
            // Do not register the generic map error listener (circular payloads).
            await _map.AddAsyncListener<JsonElement>("dblclick", OnMapDoubleClickedAsync);
            await _map.AddAsyncListener<JsonElement>("click", OnMapClickedAsync);
            await _map.AddAsyncListener<JsonElement>("dragend", OnMapDraggedAsync);
        }
        catch (JSException exception)
        {
            LogMapOverlayFailure(Logger, exception);
            _hasOverlayError = true;
        }

        _mapLoaded = true;
        _mapLoadTimedOut = false;
        await InvokeAsync(StateHasChanged);
    }

    private Task OnMapClickedAsync(JsonElement click)
    {
        GeoCoordinate? point = ReadMapClickCoordinate(click);
        return !_disposed && point is { } coordinate
            ? OnBackgroundClicked.InvokeAsync(coordinate)
            : Task.CompletedTask;
    }

    public async Task<GeoCoordinate?> GetCurrentCenterAsync()
    {
        if (_disposed || !_mapLoaded || _map is null)
        {
            return null;
        }

        LngLat center = await _map.GetCenter();
        GeoCoordinate coordinate = new(center.Latitude, center.Longitude);
        return coordinate.IsValid ? coordinate : null;
    }

    private async Task OnMapDraggedAsync(JsonElement _)
    {
        GeoCoordinate? center = await GetCurrentCenterAsync();
        if (!_disposed && center is { } point)
        {
            await OnMapDragged.InvokeAsync(point);
        }
    }

    private Task OnMapDoubleClickedAsync(JsonElement _) => !_disposed
        ? OnBackgroundDoubleClicked.InvokeAsync()
        : Task.CompletedTask;

    /// <summary>Read only the map coordinate, not MapLibre's full event object.</summary>
    internal static GeoCoordinate? ReadMapClickCoordinate(JsonElement click)
    {
        if (click.ValueKind != JsonValueKind.Object ||
            !click.TryGetProperty("lngLat", out JsonElement point) || point.ValueKind != JsonValueKind.Object ||
            !point.TryGetProperty("lat", out JsonElement latitude) || latitude.ValueKind != JsonValueKind.Number ||
            !latitude.TryGetDouble(out double lat) ||
            !point.TryGetProperty("lng", out JsonElement longitude) || longitude.ValueKind != JsonValueKind.Number ||
            !longitude.TryGetDouble(out double lon))
        {
            return null;
        }

        GeoCoordinate coordinate = new(lat, lon);
        return coordinate.IsValid ? coordinate : null;
    }

    private Task SelectKnownMapFeatureAsync(GeoJsonFeature feature)
    {
        // A marker can still be visible for a frame while a new committed
        // query is replacing its source. Never promote that stale feature.
        string id = feature.Properties.Id;
        bool known = SearchState.Results.Any(result => result.Properties.Id == id) ||
                     SearchState.SelectedFeature?.Properties.Id == id;
        return known && !_disposed
            ? OnFeatureSelected.InvokeAsync(feature)
            : Task.CompletedTask;
    }

    // The canvas never resizes with the search pane. On portrait phones, place
    // a selected point in the exposed upper portion rather than behind the
    // expanded bottom sheet. This uses MudBlazor's managed viewport service,
    // without additional application-specific JavaScript or listeners.
    private async Task<double> GetSelectionOffsetPixelsAsync()
    {
        if (!SearchPanelExpanded || _disposed || await Viewport.GetCurrentBreakpointAsync() != Breakpoint.Xs)
        {
            return 0;
        }

        BrowserWindowSize size = await Viewport.GetCurrentBrowserWindowSizeAsync();
        // With the portrait bottom sheet at 68% of the map workspace,
        // shift the point into the upper, unobstructed portion. Use a
        // viewport-based distance without resizing MapLibre's canvas.
        return size.Height > size.Width ? size.Height * 0.30 : 0;
    }

    private async Task UpdateOverlayAsync()
    {
        if (_resultOverlay is null || _disposed)
        {
            return;
        }

        try
        {
            await _resultOverlay.ApplyAsync(SearchState, ThemePreferences.IsDarkMode);
            _hasOverlayError = false;
        }
        catch (JSException exception) when (!_disposed)
        {
            LogMapOverlayFailure(Logger, exception);
            _hasOverlayError = true;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Could not load or validate map style at {StyleUrl}")]
    private static partial void LogMapStyleFailure(ILogger logger, Exception exception, string styleUrl);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "Could not initialize the map browser module")]
    private static partial void LogMapBrowserFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error,
        Message = "Could not synchronize map result markers")]
    private static partial void LogMapOverlayFailure(ILogger logger, Exception exception);

    private void OnThemeChanged(object? sender, EventArgs args) => _ = InvokeAsync(UpdateOverlayAsync);

    private void Reload() => Navigation.NavigateTo(Navigation.Uri, forceLoad: true);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (ThemePreferences is { } preferences)
        {
            preferences.Changed -= OnThemeChanged;
        }
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
