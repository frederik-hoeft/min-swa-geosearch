namespace GeoSearch.Web.Client.Search.Model;

public sealed record FailedSearch(string Message) : SearchLoadState;
