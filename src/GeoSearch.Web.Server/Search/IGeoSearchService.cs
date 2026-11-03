using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Server.Search;

/// <summary>Application-level geospatial search queries exposed through the HTTP API.</summary>
public interface IGeoSearchService
{
    Task<GeoJsonFeatureCollection> SearchAsync(string query, int limit, CancellationToken cancellationToken);
    Task<GeoJsonFeatureCollection> ReverseAsync(double latitude, double longitude, double radius, CancellationToken cancellationToken);
    Task<GeoJsonFeatureCollection> NearbyAsync(double latitude, double longitude, double radius, string? category, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken cancellationToken);
}
