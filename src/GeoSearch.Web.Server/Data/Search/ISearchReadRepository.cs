namespace GeoSearch.Web.Server.Data.Search;

/// <summary>Read-side boundary for the instructor-owned GIS search model.</summary>
internal interface ISearchReadRepository
{
    Task<SearchReadResult> SearchAsync(string query, int limit, CancellationToken cancellationToken);
    Task<SearchReadResult> ReverseAsync(double latitude, double longitude, double radius, CancellationToken cancellationToken);
    Task<SearchReadResult> NearbyAsync(double latitude, double longitude, double radius, string? category, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken cancellationToken);
}
