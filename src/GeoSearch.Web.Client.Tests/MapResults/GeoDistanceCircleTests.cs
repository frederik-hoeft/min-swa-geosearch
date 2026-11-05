using Community.Blazor.MapLibre.Models.Feature;
using Community.Blazor.MapLibre.Models.Sources;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.UI.Components.Map;
using System.Text.Json;

namespace GeoSearch.Web.Client.Tests.MapResults;

[TestClass]
public sealed class GeoDistanceCircleTests
{
    [TestMethod]
    public void RadiusProducesClosedLongitudeLatitudePolygonInMeters()
    {
        GeoCoordinate origin = new(52.3759, 9.732);
        FeatureCollection circle = GeoDistanceCircle.Create(origin, 500);
        PolygonGeometry polygon = (PolygonGeometry)((FeatureFeature)circle.Features.Single()).Geometry;
        double[][] ring = polygon.Coordinates.Single();
        Assert.HasCount(97, ring);
        CollectionAssert.AreEqual(ring[0], ring[^1]);
        // North, east, south, and west all remain at the requested radius.
        foreach (int index in new[] { 0, 24, 48, 72 })
        {
            Assert.AreEqual(500, Distance(origin, ring[index]), 0.001);
        }
        Assert.IsTrue(ring[48][1] < origin.Latitude);
        Assert.IsTrue(ring[0][1] > origin.Latitude);
        Assert.IsTrue(ring[24][0] < origin.Longitude);
    }

    [TestMethod]
    public void RadiusUsesGeographicGeometryNotMapZoom()
    {
        FeatureCollection circle = GeoDistanceCircle.Create(new GeoCoordinate(52.3759, 9.732), 2000);
        IFeature feature = circle;
        string json = JsonSerializer.Serialize(feature);
        StringAssert.Contains(json, "\"type\":\"FeatureCollection\"", StringComparison.Ordinal);
        StringAssert.Contains(json, "\"type\":\"Polygon\"", StringComparison.Ordinal);
        PolygonGeometry polygon = (PolygonGeometry)((FeatureFeature)circle.Features.Single()).Geometry;
        Assert.AreEqual(2000, Distance(new GeoCoordinate(52.3759, 9.732), polygon.Coordinates[0][36]), 0.001);
    }

    [TestMethod]
    public void RingHasGeoJsonCounterclockwiseWinding()
    {
        double[][] ring = ((PolygonGeometry)((FeatureFeature)GeoDistanceCircle.Create(
            new GeoCoordinate(52.3759, 9.732), 500).Features.Single()).Geometry).Coordinates.Single();
        // A positive shoelace sum indicates counterclockwise winding in (lon,lat).
        double signedArea = 0;
        for (int i = 0; i < ring.Length - 1; i++)
        {
            signedArea += ring[i][0] * ring[i + 1][1] - ring[i + 1][0] * ring[i][1];
        }
        Assert.IsTrue(signedArea > 0);
    }

    [TestMethod]
    public void MapLibreSourceSerializesAsTypedGeoJsonPolygon()
    {
        ISource source = MapRangeOverlay.CreateSource(GeoDistanceCircle.Create(
            new GeoCoordinate(52.3759, 9.732), 500));
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(source));
        JsonElement root = json.RootElement;
        Assert.AreEqual("geojson", root.GetProperty("type").GetString());
        JsonElement data = root.GetProperty("data");
        Assert.AreEqual("FeatureCollection", data.GetProperty("type").GetString());
        JsonElement feature = data.GetProperty("features")[0];
        Assert.AreEqual("Polygon", feature.GetProperty("geometry").GetProperty("type").GetString());
        Assert.AreEqual(97, feature.GetProperty("geometry").GetProperty("coordinates")[0].GetArrayLength());
    }

    [TestMethod]
    public void InvalidAndZeroRadiusReturnEmptyCollection()
    {
        Assert.IsEmpty(GeoDistanceCircle.Create(new GeoCoordinate(52.4, 9.7), 0).Features);
        Assert.IsEmpty(GeoDistanceCircle.Create(new GeoCoordinate(52.4, 9.7), double.NaN).Features);
        Assert.IsEmpty(GeoDistanceCircle.Create(new GeoCoordinate(double.NaN, 9.7), 500).Features);
    }

    [TestMethod]
    public void CrossingAntimeridianKeepsCoordinatesContinuous()
    {
        FeatureCollection circle = GeoDistanceCircle.Create(new GeoCoordinate(0, 179.999), 2000);
        double[][] ring = ((PolygonGeometry)((FeatureFeature)circle.Features.Single()).Geometry).Coordinates.Single();
        Assert.IsTrue(ring.Any(point => point[0] > 180));
        Assert.IsTrue(ring.Zip(ring.Skip(1)).All(pair => Math.Abs(pair.First[0] - pair.Second[0]) < 1));
    }

    private static double Distance(GeoCoordinate center, double[] point)
    {
        double lat1 = center.Latitude * Math.PI / 180;
        double lat2 = point[1] * Math.PI / 180;
        double deltaLat = lat2 - lat1;
        double deltaLon = (point[0] - center.Longitude) * Math.PI / 180;
        double a = Math.Pow(Math.Sin(deltaLat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(deltaLon / 2), 2);
        return 2 * 6371008.8 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}
