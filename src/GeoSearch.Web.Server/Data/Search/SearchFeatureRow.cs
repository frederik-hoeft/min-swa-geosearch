namespace GeoSearch.Web.Server.Data.Search;

/// <summary>Keyless projection returned by the three PostGIS API read functions.</summary>
[SearchReadModel]
internal sealed partial class SearchFeatureRow
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Category { get; set; } = null!;
    public float? Score { get; set; }
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public double? DistanceM { get; set; }
    public string SnapshotId { get; set; } = null!;
}
