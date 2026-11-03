using GeoSearch.Web.Server.Data;
using Microsoft.EntityFrameworkCore;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace GeoSearch.Web.Server.Data.Search;

/// <summary>
/// Executes instructor-owned search functions and spatial queries in WKG read-only,
/// automatically rolled-back transactions. SQL input is always bound as parameters.
/// </summary>
internal sealed class SearchReadRepository(ITransactionServiceHandle transactionService)
    : DatabaseService<SearchReadDbContext>(transactionService), ISearchReadRepository
{
    public Task<SearchReadResult> SearchAsync(string query, int limit, CancellationToken cancellationToken) =>
        Transaction.Scoped.RunReadOnlyAsync<SearchReadResult>(async (context, ct) =>
        {
            List<SearchFeatureRow> rows = await context.Set<SearchFeatureRow>()
                .FromSqlInterpolated($"""
                    SELECT r.id, r.name, r.label, r.type, r.category, r.score,
                           r.lon, r.lat, r.snapshot_id,
                           NULL::double precision AS distance_m
                    FROM search.search({query}, {limit}) AS r
                    """)
                .AsNoTracking()
                .ToListAsync(ct);
            return await WrapAsync(context, rows, ct);
        }, cancellationToken);

    public Task<SearchReadResult> ReverseAsync(double latitude, double longitude, double radius, CancellationToken cancellationToken) =>
        Transaction.Scoped.RunReadOnlyAsync<SearchReadResult>(async (context, ct) =>
        {
            // Reverse returns only identity, labels, type and distance; recover the point,
            // category and snapshot from the same indexed search document.
            List<SearchFeatureRow> rows = await context.Set<SearchFeatureRow>()
                .FromSqlInterpolated($"""
                    SELECT r.id, r.name, r.label, r.type, s.label_de AS category,
                           NULL::real AS score,
                           ST_X(s.display_pt) AS lon, ST_Y(s.display_pt) AS lat,
                           s.snapshot_id, r.distance_m
                    FROM search.reverse({longitude}, {latitude}, {radius}, 1) AS r
                    JOIN search.search_index AS s ON s.id = r.id
                    """)
                .AsNoTracking()
                .ToListAsync(ct);
            return await WrapAsync(context, rows, ct);
        }, cancellationToken);

    public Task<SearchReadResult> NearbyAsync(double latitude, double longitude, double radius, string? category, int limit, CancellationToken cancellationToken) =>
        Transaction.Scoped.RunReadOnlyAsync<SearchReadResult>(async (context, ct) =>
        {
            List<SearchFeatureRow> rows = await context.Set<SearchFeatureRow>()
                .FromSqlInterpolated($"""
                    SELECT s.id, s.name,
                           search.label(s.name, s.ref, s.district, s.municipality) AS label,
                           s.type, s.label_de AS category, NULL::real AS score,
                           ST_X(s.display_pt) AS lon, ST_Y(s.display_pt) AS lat,
                           s.snapshot_id,
                           ST_Distance(s.display_pt::geography, p.pt::geography) AS distance_m
                    FROM search.search_index AS s
                    CROSS JOIN (SELECT ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326) AS pt) AS p
                    WHERE s.type = 'poi'
                      AND ({category}::text IS NULL OR s.label_de = {category})
                      AND ST_DWithin(s.display_pt::geography, p.pt::geography, {radius})
                    ORDER BY distance_m, s.id
                    LIMIT {limit}
                    """)
                .AsNoTracking()
                .ToListAsync(ct);
            return await WrapAsync(context, rows, ct);
        }, cancellationToken);

    public Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken cancellationToken) =>
        Transaction.Scoped.RunReadOnlyAsync<IReadOnlyList<string>>(async (context, ct) =>
            await context.Database.SqlQueryRaw<string>("""
                SELECT DISTINCT s.label_de AS "Value"
                FROM search.search_index AS s
                WHERE s.type = 'poi' AND s.label_de IS NOT NULL
                ORDER BY "Value"
                """).ToListAsync(ct), cancellationToken);

    private static async Task<SearchReadResult> WrapAsync(SearchReadDbContext context, List<SearchFeatureRow> rows, CancellationToken cancellationToken)
    {
        // Use the indexed snapshot, not osm.snapshot, when returning derived features.
        // For an empty view, the import metadata is the only available snapshot source.
        string snapshot = rows.Count > 0
            ? rows[0].SnapshotId
            : await context.Database.SqlQueryRaw<string>("""
                SELECT COALESCE(
                    (SELECT snapshot_id FROM search.search_index LIMIT 1),
                    (SELECT snapshot_id FROM osm.snapshot ORDER BY imported_at DESC LIMIT 1),
                    'unknown') AS "Value"
                """).SingleAsync(cancellationToken);

        return new SearchReadResult(snapshot, rows);
    }
}
