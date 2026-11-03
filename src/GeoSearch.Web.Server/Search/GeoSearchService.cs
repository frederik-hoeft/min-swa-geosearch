using GeoSearch.Web.Server.Data.Search;
using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Server.Search;

/// <summary>Translates spatial read models into the stable, point-only GeoJSON API contract.</summary>
internal sealed class GeoSearchService(ISearchReadRepository queries) : IGeoSearchService
{
    public async Task<GeoJsonFeatureCollection> SearchAsync(string query, int limit, CancellationToken cancellationToken) =>
        ToGeoJson(await queries.SearchAsync(query, limit, cancellationToken));

    public async Task<GeoJsonFeatureCollection> ReverseAsync(double latitude, double longitude, double radius, CancellationToken cancellationToken) =>
        ToGeoJson(await queries.ReverseAsync(latitude, longitude, radius, cancellationToken));

    public async Task<GeoJsonFeatureCollection> NearbyAsync(double latitude, double longitude, double radius, string? category, int limit, CancellationToken cancellationToken) =>
        ToGeoJson(await queries.NearbyAsync(latitude, longitude, radius, category, limit, cancellationToken));

    public Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken cancellationToken) =>
        queries.CategoriesAsync(cancellationToken);

    private static GeoJsonFeatureCollection ToGeoJson(SearchReadResult result)
    {
        GeoJsonFeature[] features = [.. result.Features.Select(static row =>
            new GeoJsonFeature(
                new GeoJsonPoint(row.Longitude, row.Latitude),
                new GeoJsonProperties(row.Id, row.Name, row.Label, row.Type, row.Category, row.Score, row.DistanceM)))];

        return new GeoJsonFeatureCollection(result.Snapshot, features);
    }
}
