namespace GeoSearch.Web.Client.Services.Search;

internal sealed record SearchFailure(string Message) : SearchFetchResult;
