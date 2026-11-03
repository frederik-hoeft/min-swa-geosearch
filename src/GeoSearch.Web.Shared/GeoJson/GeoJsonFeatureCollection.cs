using System.Text.Json.Serialization;

namespace GeoSearch.Web.Shared.GeoJson;

/// <summary>A point-only GeoJSON feature collection with OSM snapshot provenance.</summary>
#pragma warning disable CA1711 // FeatureCollection is the RFC 7946 GeoJSON type name.
public sealed record GeoJsonFeatureCollection(
    [property: JsonPropertyName("snapshot")] string Snapshot,
    [property: JsonPropertyName("features")] IReadOnlyList<GeoJsonFeature> Features)
{
    [JsonPropertyName("type")]
    public string Type { get; } = "FeatureCollection";

    [JsonPropertyName("attribution")]
    public string Attribution { get; } = "© OpenStreetMap contributors, ODbL";
}
#pragma warning restore CA1711
