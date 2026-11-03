using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace GeoSearch.Web.Server.Data.Search;

internal sealed partial class OsmSnapshotRow : IDiscoverableModelConfiguration<OsmSnapshotRow>
{
    public static void Configure(EntityTypeBuilder<OsmSnapshotRow> self)
    {
        ArgumentNullException.ThrowIfNull(self);
        self.ToView("snapshot", "osm")
            .HasNoKey();

        self.Property(static row => row.SnapshotId)
            .HasColumnName("snapshot_id")
            .HasColumnType("text");

        self.Property(static row => row.ImportedAt)
            .HasColumnName("imported_at")
            .HasColumnType("timestamp with time zone");
    }
}
