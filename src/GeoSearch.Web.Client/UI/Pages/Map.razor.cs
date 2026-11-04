using GeoSearch.Web.Client.UI.Components;
using Microsoft.AspNetCore.Components;

namespace GeoSearch.Web.Client.UI.Pages;

public partial class Map : ComponentBase
{
    private MapViewer? _mapViewer;
    private bool _isSearchPanelExpanded = true;
    private bool _mapResizeRequested;

    private void ToggleSearchPanel()
    {
        _isSearchPanelExpanded = !_isSearchPanelExpanded;
        _mapResizeRequested = true;
    }

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_mapResizeRequested || _mapViewer is null)
        {
            return;
        }

        _mapResizeRequested = false;
        await _mapViewer.ResizeAsync();
    }
}
