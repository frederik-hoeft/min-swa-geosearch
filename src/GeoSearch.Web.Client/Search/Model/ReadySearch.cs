using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Client.Search.Model;

public sealed record ReadySearch(GeoJsonFeatureCollection Collection) : SearchLoadState;
