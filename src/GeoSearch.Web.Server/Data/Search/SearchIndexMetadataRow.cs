namespace GeoSearch.Web.Server.Data.Search;

/// <summary>Minimal projection of the materialized index for category and snapshot lookups.</summary>
[SearchReadModel]
internal sealed partial class SearchIndexMetadataRow
{
    public string Type { get; set; } = null!;
    public string? Category { get; set; }
    public string SnapshotId { get; set; } = null!;
}
