using Community.Blazor.MapLibre.Models.Event;
using Community.Blazor.MapLibre.Models.Feature;
using Community.Blazor.MapLibre.Models.LayerFeatures;
using Community.Blazor.MapLibre.Models.Sources;
using GeoSearch.Web.Client.UI.Components.Map;
using GeoSearch.Web.Shared.GeoJson;
using OneOf;
using System.Text.Json;

namespace GeoSearch.Web.Client.Tests.MapResults;

[TestClass]
public sealed class MapResultOverlayTests
{
    [TestMethod]
    public void ResultSourceUsesGeoJsonFeatureIdentityAndLongitudeLatitudeOrder()
    {
        GeoJsonFeature feature = Feature("osm:1", 9.732, 52.376);
        FeatureCollection collection = MapResultOverlay.ToFeatureCollection([feature]);

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize<IFeature>(collection));
        Assert.AreEqual("FeatureCollection", json.RootElement.GetProperty("type").GetString());
        JsonElement result = json.RootElement.GetProperty("features")[0];
        Assert.AreEqual("Feature", result.GetProperty("type").GetString());
        Assert.AreEqual("osm:1", result.GetProperty("id").GetString());
        Assert.AreEqual("Point", result.GetProperty("geometry").GetProperty("type").GetString());
        JsonElement coordinates = result.GetProperty("geometry").GetProperty("coordinates");
        Assert.AreEqual(9.732, coordinates[0].GetDouble());
        Assert.AreEqual(52.376, coordinates[1].GetDouble());
    }

    [TestMethod]
    public void WrapperSerializesGeoJsonSourceWithTypedFeatureCollection()
    {
        GeoJsonSource source = new()
        {
            Data = OneOf<IFeature, string>.FromT0(MapResultOverlay.ToFeatureCollection([Feature("osm:1", 9.732, 52.376)]))
        };

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize<ISource>(source));
        Assert.AreEqual("geojson", json.RootElement.GetProperty("type").GetString());
        Assert.AreEqual("FeatureCollection", json.RootElement.GetProperty("data").GetProperty("type").GetString());
        Assert.HasCount(1, json.RootElement.GetProperty("data").GetProperty("features").EnumerateArray().ToArray());
    }

    [TestMethod]
    public void WrapperCanReadClickedLayerFeatureIdentity()
    {
        MapMouseEvent? clicked = JsonSerializer.Deserialize<MapMouseEvent>("""
            {
              "point": { "x": 120, "y": 45 },
              "lngLat": { "lng": 9.732, "lat": 52.376 },
              "type": "click",
              "features": [
                {
                  "type": "Feature", "source": "gs-search-results", "id": "osm:1",
                  "geometry": { "type": "Point", "coordinates": [9.732, 52.376] },
                  "properties": { "id": "osm:1" }
                }
              ]
            }
            """);

        Assert.IsNotNull(clicked);
        Assert.HasCount(1, clicked.Features);
        Assert.AreEqual("osm:1", ((LayerFeatureFeature)clicked.Features[0]).Id);
    }

    [TestMethod]
    public void ClickedMapLibreFeatureIdIsReadWithoutRequiringFullEventBinding()
    {
        using JsonDocument clicked = JsonDocument.Parse("""
            {
              "type": "click", "lngLat": { "lng": 9.732, "lat": 52.376 },
              "features": [
                { "type": "Feature", "id": "osm:1", "properties": { "id": "osm:1" } }
              ]
            }
            """);

        Assert.AreEqual("osm:1", MapResultOverlay.ClickedFeatureId(clicked.RootElement));
    }

    [TestMethod]
    public void ClickedMapLibreFeatureIdSupportsPropertyFallbackAndMissingFeatures()
    {
        using JsonDocument fallback = JsonDocument.Parse("""
            {"features": [{"properties": {"id": "osm:2"}}]}
            """);
        using JsonDocument empty = JsonDocument.Parse("""{"type":"click"}""");
        using JsonDocument numeric = JsonDocument.Parse("""{"features":[{"id":123}]}""");

        using JsonDocument coerced = JsonDocument.Parse("""
            {"features":[{"id":42,"properties":{"id":"osm:2"}}]}
            """);

        Assert.AreEqual("osm:2", MapResultOverlay.ClickedFeatureId(fallback.RootElement));
        Assert.AreEqual("osm:2", MapResultOverlay.ClickedFeatureId(coerced.RootElement));
        Assert.AreEqual("123", MapResultOverlay.ClickedFeatureId(numeric.RootElement));
        Assert.IsNull(MapResultOverlay.ClickedFeatureId(empty.RootElement));
    }

    [TestMethod]
    public void CustomPinSymbolUsesBottomAnchorAndFullVisibility()
    {
        // The renderer uses SymbolLayer rather than CircleLayer; its click targets
        // correspond to the actual rendered pin imagery.
        Community.Blazor.MapLibre.Models.Layers.SymbolLayer layer = new()
        {
            Id = "test",
            Source = "test",
            Layout = new Community.Blazor.MapLibre.Models.Layers.SymbolLayerLayout
            {
                IconImage = "pin",
                IconAnchor = "bottom",
                IconAllowOverlap = true,
                IconIgnorePlacement = true
            }
        };

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize<Community.Blazor.MapLibre.Models.Layers.Layer>(layer));
        Assert.AreEqual("symbol", json.RootElement.GetProperty("type").GetString());
        Assert.AreEqual("bottom", json.RootElement.GetProperty("layout").GetProperty("icon-anchor").GetString());
        Assert.AreEqual("pin", json.RootElement.GetProperty("layout").GetProperty("icon-image").GetString());
    }

    [TestMethod]
    public void SelectedMarkerIsExcludedFromResultLayerAndInvalidCoordinatesAreIgnored()
    {
        GeoJsonFeature first = Feature("osm:1", 9.732, 52.376);
        GeoJsonFeature second = Feature("osm:2", 9.711, 52.4);
        GeoJsonFeature invalid = Feature("osm:3", double.NaN, 52.4);
        GeoJsonFeatureCollection unused = new("snapshot", [first, second, invalid]);

        FeatureCollection layer = MapResultOverlay.ToFeatureCollection(unused.Features, first.Properties.Id);
        Assert.HasCount(1, layer.Features);
        Assert.AreEqual("osm:2", ((FeatureFeature)layer.Features[0]).Id);
    }

    private static GeoJsonFeature Feature(string id, double longitude, double latitude) =>
        new(new GeoJsonPoint(longitude, latitude), new GeoJsonProperties(id, id, id, "poi", "", null, null));
}
