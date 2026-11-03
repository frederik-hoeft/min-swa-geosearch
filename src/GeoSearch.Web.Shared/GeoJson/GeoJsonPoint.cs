using System.Text.Json.Serialization;

namespace GeoSearch.Web.Shared.GeoJson;

/// <summary>WGS 84 position in GeoJSON longitude, latitude order (RFC 7946).</summary>
public sealed record GeoJsonPoint
{
    public GeoJsonPoint(double longitude, double latitude)
    {
        Coordinates = [longitude, latitude];
    }

    [JsonPropertyName("type")]
    public string Type { get; } = "Point";

    [JsonPropertyName("coordinates")]
    public IReadOnlyList<double> Coordinates { get; }
}
