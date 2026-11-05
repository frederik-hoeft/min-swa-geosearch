namespace GeoSearch.Web.Client.Search.Model;

/// <summary>Foreground inspection of a clicked coordinate, layered over committed discovery.</summary>
public sealed record ReverseInspectionPanel(
    GeoCoordinate Point, SearchLoadState Result, SearchPanelState ReturnTo, string ReturnText) : SearchPanelState;
