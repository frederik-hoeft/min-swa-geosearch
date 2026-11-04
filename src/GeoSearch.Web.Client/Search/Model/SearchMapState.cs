using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Client.Search.Model;

/// <summary>
/// Read-only map projection of the committed search panel. Draft autocomplete
/// suggestions never become map markers, and inspection never loses the
/// committed result set beneath it.
/// </summary>
public sealed record SearchMapState(IReadOnlyList<GeoJsonFeature> Results, GeoJsonFeature? SelectedFeature)
{
    public static SearchMapState Empty { get; } = new([], null);

    public static SearchMapState FromPanel(SearchPanelState panel)
    {
        GeoJsonFeature? selected = null;
        while (panel is PlaceSearchPanel place)
        {
            selected ??= place.Feature;
            panel = place.ReturnTo;
        }

        IReadOnlyList<GeoJsonFeature> results = panel is ForwardSearchPanel { Results: ReadySearch ready }
            ? ready.Collection.Features
            : [];

        return new SearchMapState(results, selected);
    }
}
