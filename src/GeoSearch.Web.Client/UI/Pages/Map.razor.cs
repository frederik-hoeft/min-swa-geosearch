using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GeoSearch.Web.Client.UI.Pages;

public partial class Map : ComponentBase, IAsyncDisposable
{
    private ElementReference _searchPanel;
    private IJSObjectReference? _module;
    private IJSObjectReference? _observer;
    private DotNetObjectReference<Map>? _self;
    private bool _isSearchPanelExpanded = true;
    private bool _temporaryInteractionExpanded;
    private bool _disposed;

    // F2 will consume this value; typing alone does not send API requests yet.
    private string? _searchQuery;

    private bool IsEffectivelyExpanded => _isSearchPanelExpanded || _temporaryInteractionExpanded;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./UI/Pages/Map.razor.js");
        _self = DotNetObjectReference.Create(this);
        _observer = await _module.InvokeAsync<IJSObjectReference>("observeSearchPanel", _searchPanel, _self);
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
        try
        {
            if (_observer is not null)
            {
                await _observer.InvokeVoidAsync("dispose");
                await _observer.DisposeAsync();
            }

            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // Browser disconnected while navigating away.
        }
        finally
        {
            _self?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
