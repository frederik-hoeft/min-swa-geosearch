using GeoSearch.Web.Server.Data.Search;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using Wkg.EntityFrameworkCore.Configuration.Policies.Defaults.EntityNamingPolicies;
using Wkg.EntityFrameworkCore.Configuration.Policies.Defaults.PropertyMappingPolicies;
using Wkg.EntityFrameworkCore.Extensions;

namespace GeoSearch.Web.Server.Data;

/// <summary>
/// Read-only projection model for instructor-owned PostGIS data. Contains no migrations or
/// tracked entities; the database role independently enforces read-only access.
/// </summary>
public sealed class SearchReadDbContext(DbContextOptions<SearchReadDbContext> options, SearchReadModelLoader modelLoader)
    : DbContext(options)
{
    /// <summary>Calls the full-text/fuzzy search function through a composable EF query root.</summary>
    [DbFunction("api_search", "search")]
    internal IQueryable<SearchFeatureRow> SearchFeatures(string query, int limit) =>
        FromExpression(() => SearchFeatures(query, limit));

    /// <summary>Calls the reverse lookup and resolves its matching indexed geometry.</summary>
    [DbFunction("api_reverse", "search")]
    internal IQueryable<SearchFeatureRow> ReverseFeatures(double longitude, double latitude, double radius) =>
        FromExpression(() => ReverseFeatures(longitude, latitude, radius));

    /// <summary>Queries POIs by PostGIS metre radius and distance sort.</summary>
    [DbFunction("api_nearby", "search")]
    internal IQueryable<SearchFeatureRow> NearbyFeatures(double longitude, double latitude, double radius, string? category, int limit) =>
        FromExpression(() => NearbyFeatures(longitude, latitude, radius, category, limit));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.LoadModels(modelLoader, options => options
            .ConfigurePolicies(policies => policies
                .AddPolicy<EntityNaming>(policy => policy.RequireExplicit())
                .AddPolicy<PropertyMapping>(policy => policy.RequireExplicit())));
    }

    public override int SaveChanges() => ThrowReadOnly<int>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => ThrowReadOnly<int>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => ThrowReadOnly<Task<int>>();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) => ThrowReadOnly<Task<int>>();

    [DoesNotReturn]
    private static T ThrowReadOnly<T>() => throw new NotSupportedException("The search database is read-only.");
}
