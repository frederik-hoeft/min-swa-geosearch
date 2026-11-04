namespace GeoSearch.Web.Client.Search.Model;

public sealed record ForwardSearchPanel(string Query, SearchLoadState Results, int Limit = 10) : SearchPanelState;
