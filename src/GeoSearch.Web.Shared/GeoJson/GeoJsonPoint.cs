using System.Text.Json.Serialization;

namespace GeoSearch.Web.Shared.GeoJson;

/// <summary>WGS 84 position in GeoJSON longitude, latitude order (RFC 7946).</summary>
public sealed record GeoJsonPoint
{
    public GeoJsonPoint(double longitude, double latitude) : this([longitude, latitude])
    {
    }

    // JSON contains a "coordinates" array, not separate longitude/latitude members.
    // System.Text.Json constructor parameters must match a CLR property or field.
    [JsonConstructor]
    public GeoJsonPoint(IReadOnlyList<double> coordinates)
    {
        Coordinates = coordinates;
    }

    [JsonPropertyName("type")]
    public string Type { get; } = "Point";

    [JsonPropertyName("coordinates")]
    public IReadOnlyList<double> Coordinates { get; }
}
