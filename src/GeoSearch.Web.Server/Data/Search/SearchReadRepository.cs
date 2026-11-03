using GeoSearch.Web.Server.Data;
using Microsoft.EntityFrameworkCore;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace GeoSearch.Web.Server.Data.Search;

/// <summary>
/// Reads the PostGIS query functions using composable EF queries in WKG read-only,
/// auto-rollback transactions. Spatial evaluation remains entirely database-side.
/// </summary>
internal sealed class SearchReadRepository(ITransactionServiceHandle transactionService)
    : DatabaseService<SearchReadDbContext>(transactionService), ISearchReadRepository
{
    public Task<SearchReadResult> SearchAsync(string query, int limit, CancellationToken cancellationToken) =>
        Transaction.Scoped.RunReadOnlyAsync<SearchReadResult>(async (context, ct) =>
        {
            List<SearchFeatureRow> rows = await context.SearchFeatures(query, limit)
                .OrderByDescending(static row => row.Score).ThenBy(static row => row.Id)
                .AsNoTracking().ToListAsync(ct);
            return await WrapAsync(context, rows, ct);
        }, cancellationToken);

    public Task<SearchReadResult> ReverseAsync(double latitude, double longitude, double radius, CancellationToken cancellationToken) =>
        Transaction.Scoped.RunReadOnlyAsync<SearchReadResult>(async (context, ct) =>
        {
            List<SearchFeatureRow> rows = await context.ReverseFeatures(longitude, latitude, radius)
                .OrderBy(static row => row.DistanceM).ThenBy(static row => row.Id)
                .AsNoTracking().ToListAsync(ct);
            return await WrapAsync(context, rows, ct);
        }, cancellationToken);

    public Task<SearchReadResult> NearbyAsync(double latitude, double longitude, double radius, string? category, int limit, CancellationToken cancellationToken) =>
        Transaction.Scoped.RunReadOnlyAsync<SearchReadResult>(async (context, ct) =>
        {
            List<SearchFeatureRow> rows = await context.NearbyFeatures(longitude, latitude, radius, category, limit)
                .OrderBy(static row => row.DistanceM).ThenBy(static row => row.Id)
                .AsNoTracking().ToListAsync(ct);
            return await WrapAsync(context, rows, ct);
        }, cancellationToken);

    public Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken cancellationToken) =>
        Transaction.Scoped.RunReadOnlyAsync<IReadOnlyList<string>>(async (context, ct) =>
            await context.Set<SearchIndexMetadataRow>()
                .Where(static row => row.Type == "poi" && row.Category != null)
                .Select(static row => row.Category!)
                .Distinct()
                .OrderBy(static category => category)
                .ToListAsync(ct), cancellationToken);

    private static async Task<SearchReadResult> WrapAsync(SearchReadDbContext context, List<SearchFeatureRow> rows, CancellationToken cancellationToken)
    {
        string? snapshot = rows.Count > 0 ? rows[0].SnapshotId : await context.Set<SearchIndexMetadataRow>()
            .Select(static row => row.SnapshotId).FirstOrDefaultAsync(cancellationToken);
        snapshot ??= await context.Set<OsmSnapshotRow>()
            .OrderByDescending(static row => row.ImportedAt)
            .Select(static row => row.SnapshotId)
            .FirstOrDefaultAsync(cancellationToken);
        return new SearchReadResult(snapshot ?? "unknown", rows);
    }
}
