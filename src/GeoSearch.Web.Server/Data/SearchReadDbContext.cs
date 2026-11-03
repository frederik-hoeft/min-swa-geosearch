using GeoSearch.Web.Server.Data.Search;
using Microsoft.EntityFrameworkCore;

namespace GeoSearch.Web.Server.Data;

/// <summary>
/// Query-only model for instructor-owned search data. It has no migrations or tracked entities.
/// Database permissions must independently enforce the application's read-only role.
/// </summary>
public sealed class SearchReadDbContext(DbContextOptions<SearchReadDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SearchFeatureRow>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(null);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Name).HasColumnName("name");
            entity.Property(row => row.Label).HasColumnName("label");
            entity.Property(row => row.Type).HasColumnName("type");
            entity.Property(row => row.Category).HasColumnName("category");
            entity.Property(row => row.Score).HasColumnName("score");
            entity.Property(row => row.Longitude).HasColumnName("lon");
            entity.Property(row => row.Latitude).HasColumnName("lat");
            entity.Property(row => row.DistanceM).HasColumnName("distance_m");
            entity.Property(row => row.SnapshotId).HasColumnName("snapshot_id");
        });
    }

    public override int SaveChanges() => throw new NotSupportedException("The search database is read-only.");

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new NotSupportedException("The search database is read-only.");

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The search database is read-only.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The search database is read-only.");
}
