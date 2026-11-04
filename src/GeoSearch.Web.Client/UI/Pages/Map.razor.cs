using Microsoft.AspNetCore.Components;

namespace GeoSearch.Web.Client.UI.Pages;

public partial class Map : ComponentBase
{
    private bool _isSearchPanelExpanded = true;

    // F2 will consume this value; typing alone does not send API requests yet.
    private string? _searchQuery;

    private void ToggleSearchPanel() => _isSearchPanelExpanded = !_isSearchPanelExpanded;
}
