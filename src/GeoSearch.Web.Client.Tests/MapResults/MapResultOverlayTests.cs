using Community.Blazor.MapLibre.Models.Marker;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.UI.Components.Map;
using GeoSearch.Web.Shared.GeoJson;
using System.Text.Json;

namespace GeoSearch.Web.Client.Tests.MapResults;

[TestClass]
public sealed class MapResultOverlayTests
{
    [TestMethod]
    public void CommittedResultsBecomeOneMarkerPerValidFeature()
    {
        GeoJsonFeature first = Feature("osm:1", 9.732, 52.376);
        GeoJsonFeature duplicate = Feature("osm:1", 9.71, 52.4);
        GeoJsonFeature invalid = Feature("osm:2", double.NaN, 52.4);
        GeoJsonFeature other = Feature("osm:3", 9.745, 52.36);

        Dictionary<string, (GeoJsonFeature Feature, bool Selected)> projected =
            MapResultOverlay.ProjectMarkers(new SearchMapState([first, duplicate, invalid, other], null));

        Assert.HasCount(2, projected);
        Assert.AreSame(first, projected["osm:1"].Feature);
        Assert.IsFalse(projected["osm:1"].Selected);
        Assert.IsFalse(projected["osm:3"].Selected);
    }

    [TestMethod]
    public void SelectedFeatureReplacesSameIdWithoutDuplicatingIt()
    {
        GeoJsonFeature first = Feature("osm:1", 9.732, 52.376);
        GeoJsonFeature other = Feature("osm:2", 9.745, 52.36);
        Dictionary<string, (GeoJsonFeature Feature, bool Selected)> projected =
            MapResultOverlay.ProjectMarkers(new SearchMapState([first, other], first));

        Assert.HasCount(2, projected);
        Assert.AreSame(first, projected["osm:1"].Feature);
        Assert.IsTrue(projected["osm:1"].Selected);
        Assert.IsFalse(projected["osm:2"].Selected);

        Dictionary<string, (GeoJsonFeature Feature, bool Selected)> autocompleteOnly =
            MapResultOverlay.ProjectMarkers(new SearchMapState([], other));
        Assert.HasCount(1, autocompleteOnly);
        Assert.IsTrue(autocompleteOnly["osm:2"].Selected);
    }

    [TestMethod]
    public void DomPinUsesBottomAnchorAndKeyboardOperableButton()
    {
        int selected = 0;
        MarkerOptions marker = MapResultOverlay.CreateMarkerOptions(
            Feature("osm:1", 9.732, 52.376), selected: false, () => { selected++; return Task.CompletedTask; });

        Assert.AreEqual(MarkerAnchor.Bottom, marker.Anchor);
        Assert.AreEqual("gs-search-marker", marker.ClassName);
        Assert.IsNotNull(marker.Extensions);
        StringAssert.Contains(marker.Extensions.HtmlContent, "<button", StringComparison.Ordinal);
        StringAssert.Contains(marker.Extensions.HtmlContent, "type=\"button\"", StringComparison.Ordinal);
        StringAssert.Contains(marker.Extensions.HtmlContent, "viewBox=\"0 0 36 48\"", StringComparison.Ordinal);
        StringAssert.Contains(marker.Extensions.HtmlContent, "gs-map-pin__shape", StringComparison.Ordinal);
        StringAssert.Contains(marker.Extensions.HtmlContent, "aria-label=", StringComparison.Ordinal);
        Assert.IsNotNull(marker.OnClickAsync);
        marker.OnClickAsync(new MarkerEvent()).GetAwaiter().GetResult();
        Assert.AreEqual(1, selected);
    }

    [TestMethod]
    public void HtmlPinEscapesUntrustedLabelsAndUsesSeparateSelectedStyle()
    {
        GeoJsonFeature suspicious = Feature("osm:1", 9.732, 52.376, "<img src=x onerror=alert(1)> & \"test\"");
        MarkerOptions marker = MapResultOverlay.CreateMarkerOptions(suspicious, selected: true, () => Task.CompletedTask);

        Assert.AreEqual("gs-search-marker gs-search-marker--selected", marker.ClassName);
        Assert.IsNotNull(marker.Extensions);
        Assert.IsFalse(marker.Extensions.HtmlContent.Contains("<img", StringComparison.Ordinal));
        StringAssert.Contains(marker.Extensions.HtmlContent, "&lt;img", StringComparison.Ordinal);
        StringAssert.Contains(marker.Extensions.HtmlContent, "&quot;", StringComparison.Ordinal);
        StringAssert.Contains(marker.Extensions.HtmlContent, "&amp;", StringComparison.Ordinal);

        // The wrapper receives the marker extension as JSON and creates the DOM
        // element, rather than serializing the circular MapLibre map event.
        using JsonDocument serialized = JsonDocument.Parse(JsonSerializer.Serialize(marker));
        Assert.AreEqual("bottom", serialized.RootElement.GetProperty("anchor").GetString());
        StringAssert.Contains(serialized.RootElement.GetProperty("extensions").GetProperty("htmlContent").GetString()!, "gs-map-pin", StringComparison.Ordinal);
    }

    private static GeoJsonFeature Feature(string id, double longitude, double latitude, string? label = null) =>
        new(new GeoJsonPoint(longitude, latitude), new GeoJsonProperties(id, id, label ?? id, "poi", "", null, null));
}
