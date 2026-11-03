using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace GeoSearch.Web.Server.Data.Search;

internal sealed partial class SearchFeatureRow : IDiscoverableModelConfiguration<SearchFeatureRow>
{
    public static void Configure(EntityTypeBuilder<SearchFeatureRow> self)
    {
        ArgumentNullException.ThrowIfNull(self);
        self.HasNoKey();
        // This is a function result shape, not a table or view managed by EF.
        self.ToView(null);
        self.Property(static row => row.Id).HasColumnName("id").HasColumnType("text");
        self.Property(static row => row.Name).HasColumnName("name").HasColumnType("text");
        self.Property(static row => row.Label).HasColumnName("label").HasColumnType("text");
        self.Property(static row => row.Type).HasColumnName("type").HasColumnType("text");
        self.Property(static row => row.Category).HasColumnName("category").HasColumnType("text");
        self.Property(static row => row.Score).HasColumnName("score").HasColumnType("real");
        self.Property(static row => row.Longitude).HasColumnName("lon").HasColumnType("double precision");
        self.Property(static row => row.Latitude).HasColumnName("lat").HasColumnType("double precision");
        self.Property(static row => row.DistanceM).HasColumnName("distance_m").HasColumnType("double precision");
        self.Property(static row => row.SnapshotId).HasColumnName("snapshot_id").HasColumnType("text");
    }
}
