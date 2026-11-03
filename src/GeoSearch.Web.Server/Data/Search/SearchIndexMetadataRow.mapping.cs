using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wkg.EntityFrameworkCore.Configuration;

namespace GeoSearch.Web.Server.Data.Search;

internal sealed partial class SearchIndexMetadataRow : IDiscoverableModelConfiguration<SearchIndexMetadataRow>
{
    public static void Configure(EntityTypeBuilder<SearchIndexMetadataRow> self)
    {
        ArgumentNullException.ThrowIfNull(self);
        self.HasNoKey();
        self.ToView("search_index", "search");
        self.Property(static row => row.Type).HasColumnName("type").HasColumnType("text");
        self.Property(static row => row.Category).HasColumnName("label_de").HasColumnType("text");
        self.Property(static row => row.SnapshotId).HasColumnName("snapshot_id").HasColumnType("text");
    }
}
