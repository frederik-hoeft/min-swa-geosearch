using System.Text.Json.Serialization;

namespace GeoSearch.Web.Shared.GeoJson;

public sealed record GeoJsonFeature
(
    [property: JsonPropertyName("geometry")] GeoJsonPoint Geometry,
    [property: JsonPropertyName("properties")] GeoJsonProperties Properties
)
{
    [JsonPropertyName("type")]
    public string Type { get; } = "Feature";
}
