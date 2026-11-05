namespace GeoSearch.Web.Client.Search.Model;

/// <summary>One nearby editor and optionally a previous applied result snapshot.</summary>
public sealed record NearbySearchPanel(
    NearbyCriteria Draft,
    AppliedNearbyQuery? Applied,
    SearchLoadState? Results,
    bool Editing,
    SearchPanelState ReturnTo,
    string ReturnText) : SearchPanelState;
