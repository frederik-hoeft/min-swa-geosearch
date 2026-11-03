using GeoSearch.Web.Shared.GeoJson;
using System.Text.Json;

namespace GeoSearch.Web.Server.Tests.Search;

[TestClass]
public sealed class SearchContractTests
{
    [TestMethod]
    public void GeoJsonFeaturesHaveLongitudeLatitudeAndStablePropertyNames()
    {
        GeoJsonFeatureCollection response = new("2026-10-28", [new GeoJsonFeature(
            new GeoJsonPoint(9.71497, 52.36677),
            new GeoJsonProperties("pois:123", "Markt-Apotheke", "Markt-Apotheke, Hannover",
                "poi", "Apotheke", 0.18f, null))]);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(response));
        JsonElement root = document.RootElement;
        Assert.AreEqual("FeatureCollection", root.GetProperty("type").GetString());
        Assert.AreEqual("2026-10-28", root.GetProperty("snapshot").GetString());
        Assert.AreEqual("© OpenStreetMap contributors, ODbL", root.GetProperty("attribution").GetString());

        JsonElement feature = root.GetProperty("features")[0];
        Assert.AreEqual("Feature", feature.GetProperty("type").GetString());
        JsonElement geometry = feature.GetProperty("geometry");
        Assert.AreEqual("Point", geometry.GetProperty("type").GetString());
        Assert.AreEqual(9.71497, geometry.GetProperty("coordinates")[0].GetDouble());
        Assert.AreEqual(52.36677, geometry.GetProperty("coordinates")[1].GetDouble());

        JsonElement properties = feature.GetProperty("properties");
        Assert.AreEqual("pois:123", properties.GetProperty("id").GetString());
        Assert.AreEqual("Apotheke", properties.GetProperty("category").GetString());
        Assert.IsTrue(properties.TryGetProperty("distance_m", out JsonElement distance));
        Assert.AreEqual(JsonValueKind.Null, distance.ValueKind);
        Assert.AreEqual(0.18f, properties.GetProperty("score").GetSingle());
    }

    [TestMethod]
    public void ReverseAndNearbyResponseCanExpressDistanceWithoutSearchScore()
    {
        GeoJsonProperties properties = new("id", "name", "label", "poi", "Apotheke", null, 192.5);
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(properties));

        Assert.AreEqual(JsonValueKind.Null, json.RootElement.GetProperty("score").ValueKind);
        Assert.AreEqual(192.5, json.RootElement.GetProperty("distance_m").GetDouble());
    }
}
