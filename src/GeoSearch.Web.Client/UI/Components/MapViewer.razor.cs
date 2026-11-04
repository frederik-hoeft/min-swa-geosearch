using System.Text.Json;
using Community.Blazor.MapLibre;
using Community.Blazor.MapLibre.Models;
using Community.Blazor.MapLibre.Models.Control;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GeoSearch.Web.Client.UI.Components;

public partial class MapViewer : ComponentBase, IAsyncDisposable
{
    private const string DefaultStyleUrl = "map-styles/osm-standard.json";

    private MapLibre? _map;
    private IJSObjectReference? _browserModule;
    private MapOptions _options = new();
    private bool _browserChecked;
    private bool _supportsWebGl2;
    private bool _mapLoaded;
    private bool _isErrorListenerRegistered;
    private bool _hasTileError;

    [Inject]
    private IConfiguration Configuration { get; set; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    protected override void OnInitialized()
    {
        _options = new MapOptions
        {
            Style = Configuration["Map:StyleUrl"] ?? DefaultStyleUrl,
            Center = new LngLat(longitude: 9.7320, latitude: 52.3759),
            Zoom = 11.5,
            MaxZoom = 19,
            AttributionControl = new { compact = false },
            TrackResize = true,
            DragRotate = false
        };
    }

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _browserModule = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./UI/Components/MapViewer.razor.js");
        _supportsWebGl2 = await _browserModule.InvokeAsync<bool>("supportsWebGl2");
        _browserChecked = true;
        StateHasChanged();
    }

    private async Task OnStyleLoadedAsync(EventArgs _)
    {
        if (_map is null || _isErrorListenerRegistered)
        {
            return;
        }

        // The wrapper owns the listener and disposes its JS map and .NET references on teardown.
        await _map.AddAsyncListener<JsonElement>("error", OnMapErrorAsync);
        _isErrorListenerRegistered = true;
    }

    private async Task OnMapLoadedAsync(EventArgs _)
    {
        if (_map is null)
        {
            return;
        }

        await _map.AddControl(ControlType.NavigationControl, ControlPosition.TopRight, new { showCompass = false });
        _mapLoaded = true;
        StateHasChanged();
    }

    private Task OnMapErrorAsync(JsonElement _)
    {
        _hasTileError = true;
        return InvokeAsync(StateHasChanged);
    }

    private void DismissError() => _hasTileError = false;

    public async ValueTask DisposeAsync()
    {
        if (_browserModule is not null)
        {
            try
            {
                await _browserModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The browser may already have disconnected during navigation or shutdown.
            }
        }

        GC.SuppressFinalize(this);
    }
}
