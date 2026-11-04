using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Client.Services.Search;

internal sealed record SearchSuccess(GeoJsonFeatureCollection Collection) : SearchFetchResult;
