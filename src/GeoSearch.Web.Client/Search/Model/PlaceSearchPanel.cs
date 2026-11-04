using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Client.Search.Model;

public sealed record PlaceSearchPanel(GeoJsonFeature Feature, SearchPanelState ReturnTo, string ReturnText, bool ShowReturnResults = true) : SearchPanelState;
