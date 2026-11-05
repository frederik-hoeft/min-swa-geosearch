using GeoSearch.Web.Client.UI.Components.Map;

namespace GeoSearch.Web.Client.Tests.MapResults;

[TestClass]
public sealed class MapCircleScaleTests
{
    [TestMethod]
    public void RadiusIsInProjectedMetresAndChangesWithZoom()
    {
        double atZoom12 = MapCircleScale.DiameterPixels(500, 52.4, 12);
        double atZoom13 = MapCircleScale.DiameterPixels(500, 52.4, 13);
        Assert.IsGreaterThan(0, atZoom12);
        Assert.AreEqual(atZoom12 * 2, atZoom13, 0.001);
        Assert.AreEqual(0, MapCircleScale.DiameterPixels(double.NaN, 52.4, 12));
    }
}
