using Microsoft.AspNetCore.Components;

namespace GeoSearch.Web.Client.UI.Pages;

public partial class Map : ComponentBase
{
    private bool _isSearchPanelExpanded = true;

    private void ToggleSearchPanel() => _isSearchPanelExpanded = !_isSearchPanelExpanded;
}
