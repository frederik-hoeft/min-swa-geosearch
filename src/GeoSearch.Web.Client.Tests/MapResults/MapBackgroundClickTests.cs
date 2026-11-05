using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.UI.Components;
using System.Text.Json;

namespace GeoSearch.Web.Client.Tests.MapResults;

[TestClass]
public sealed class MapBackgroundClickTests
{
    [TestMethod]
    public void ExtractsLongitudeAndLatitudeFromMapLibreClickPayload()
    {
        using JsonDocument click = JsonDocument.Parse("""
            {"type":"click", "lngLat":{"lng":9.732, "lat":52.3759},
             "point":{"x":123,"y":91}, "originalEvent":{}}
            """);
        GeoCoordinate? coordinate = MapViewer.ReadMapClickCoordinate(click.RootElement);
        Assert.IsNotNull(coordinate);
        Assert.AreEqual(52.3759, coordinate.Value.Latitude);
        Assert.AreEqual(9.732, coordinate.Value.Longitude);
    }

    [TestMethod]
    public void IgnoresUnrelatedOrMalformedMapEvents()
    {
        foreach (string json in new[]
        {
            "{}", "null", "{\"lngLat\":{\"lng\":9.7}}",
            "{\"lngLat\":{\"lng\":190,\"lat\":52}}",
            "{\"lngLat\":{\"lng\":9,\"lat\":\"52\"}}"
        })
        {
            using JsonDocument click = JsonDocument.Parse(json);
            Assert.IsNull(MapViewer.ReadMapClickCoordinate(click.RootElement));
        }
    }
}
