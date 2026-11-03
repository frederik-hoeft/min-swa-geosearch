namespace GeoSearch.Web.Server.Data.Search;

/// <summary>Import metadata consulted when the materialized index is empty.</summary>
[SearchReadModel]
internal sealed partial class OsmSnapshotRow
{
    public string SnapshotId { get; set; } = null!;

    public DateTimeOffset ImportedAt { get; set; }
}
