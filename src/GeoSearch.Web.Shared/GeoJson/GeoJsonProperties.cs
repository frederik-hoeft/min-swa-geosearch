using System.Text.Json.Serialization;

namespace GeoSearch.Web.Shared.GeoJson;

public sealed record GeoJsonProperties(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("score")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] float? Score,
    [property: JsonPropertyName("distance_m")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? DistanceM);
