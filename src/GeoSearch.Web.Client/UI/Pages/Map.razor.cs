using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.UI.Components.Search;
using GeoSearch.Web.Shared.GeoJson;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GeoSearch.Web.Client.UI.Pages;

public partial class Map : ComponentBase, IAsyncDisposable
{
    private ElementReference _searchPanel;
    private SearchWorkspace? _searchWorkspace;
    private IJSObjectReference? _module;
    private IJSObjectReference? _observer;
    private DotNetObjectReference<Map>? _self;
    private bool _isSearchPanelExpanded = true;
    private bool _temporaryInteractionExpanded;
    private bool _disposed;
    private SearchMapState _searchMapState = SearchMapState.Empty;

    private bool IsEffectivelyExpanded => _isSearchPanelExpanded || _temporaryInteractionExpanded;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed)
        {
            return;
        }

        // Own resources locally until initialization is complete. Navigation can
        // dispose this component while either JS interop call is still pending.
        IJSObjectReference? module = null;
        IJSObjectReference? observer = null;
        DotNetObjectReference<Map>? self = null;
        try
        {
            module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./UI/Pages/Map.razor.js");
            if (_disposed)
            {
                return;
            }

            self = DotNetObjectReference.Create(this);
            observer = await module.InvokeAsync<IJSObjectReference>("observeSearchPanel", _searchPanel, self);
            if (_disposed)
            {
                return;
            }

            // There is no await between the disposal check and ownership transfer.
            _module = module;
            _observer = observer;
            _self = self;
            module = null;
            observer = null;
            self = null;
        }
        catch (JSDisconnectedException) when (_disposed)
        {
            // The browser disconnected while initialization was in flight.
        }
        finally
        {
            await ReleaseResourcesAsync(observer, module, self);
        }
    }

    // This is an interaction state, not a second manual preference. JavaScript
    // reports it only at the same mobile media boundaries used by the SCSS.
    [JSInvokable]
    public Task SetTemporaryExpansion(bool active)
    {
        if (_disposed || _temporaryInteractionExpanded == active)
        {
            return Task.CompletedTask;
        }

        _temporaryInteractionExpanded = active;
        return InvokeAsync(StateHasChanged);
    }

    private Task DismissSearchDraftAsync() => _searchWorkspace?.DismissDraftAsync() ?? Task.CompletedTask;

    private Task UpdateSearchMapState(SearchMapState state)
    {
        _searchMapState = state;
        return Task.CompletedTask;
    }

    private Task OnMapFeatureSelectedAsync(GeoJsonFeature feature) =>
        _searchWorkspace?.SelectMapFeatureAsync(feature) ?? Task.CompletedTask;

    private void ExpandOnCommit() => _isSearchPanelExpanded = true;

    private async Task ToggleSearchPanelAsync()
    {
        _isSearchPanelExpanded = !IsEffectivelyExpanded;
        if (_temporaryInteractionExpanded)
        {
            _temporaryInteractionExpanded = false;
            // Explicit collapse also dismisses input-driven expansion, even if
            // focus moves to the chevron button during the pointer interaction.
            if (_observer is not null)
            {
                await _observer.InvokeVoidAsync("dismiss");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        IJSObjectReference? observer = _observer;
        IJSObjectReference? module = _module;
        DotNetObjectReference<Map>? self = _self;
        _observer = null;
        _module = null;
        _self = null;

        await ReleaseResourcesAsync(observer, module, self);
        GC.SuppressFinalize(this);
    }

    private static async ValueTask ReleaseResourcesAsync(
        IJSObjectReference? observer, IJSObjectReference? module, DotNetObjectReference<Map>? self)
    {
        try
        {
            if (observer is not null)
            {
                try
                {
                    await observer.InvokeVoidAsync("dispose");
                }
                catch (JSDisconnectedException)
                {
                    // The observer's browser context has already gone away.
                }
                finally
                {
                    try
                    {
                        await observer.DisposeAsync();
                    }
                    catch (JSDisconnectedException)
                    {
                        // Nothing remains to release on the disconnected browser.
                    }
                }
            }
        }
        finally
        {
            try
            {
                if (module is not null)
                {
                    try
                    {
                        await module.DisposeAsync();
                    }
                    catch (JSDisconnectedException)
                    {
                        // The module was already discarded by the browser.
                    }
                }
            }
            finally
            {
                self?.Dispose();
            }
        }
    }
}
