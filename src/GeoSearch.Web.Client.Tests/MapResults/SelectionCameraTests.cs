using Community.Blazor.MapLibre.Models;
using GeoSearch.Web.Client.UI.Components.Map;

namespace GeoSearch.Web.Client.Tests.MapResults;

[TestClass]
public sealed class SelectionCameraTests
{
    [TestMethod]
    public void WithoutObstructingSheetSelectionKeepsExactCenter()
    {
        LngLat target = new(9.732, 52.3759);
        Assert.AreSame(target, SelectionCamera.GetCenter(target, 14.5, 0));
    }

    [TestMethod]
    [DataRow(14.5, 170)]
    [DataRow(15.75, 253.2)]
    [DataRow(17.0, 300)]
    public void SelectedPointIsShiftedUpByExpectedPixels(double zoom, double pixels)
    {
        LngLat target = new(9.732, 52.3759);
        LngLat cameraCenter = SelectionCamera.GetCenter(target, zoom, pixels);

        Assert.AreEqual(target.Longitude, cameraCenter.Longitude);
        Assert.IsLessThan(target.Latitude, cameraCenter.Latitude);

        // Convert both coordinates to their independent Web Mercator positions:
        // the selected pin must project exactly 'pixels' above the new center.
        double size = 512 * Math.Pow(2, zoom);
        double screenOffset = (MercatorY(cameraCenter.Latitude) - MercatorY(target.Latitude)) * size;
        Assert.AreEqual(pixels, screenOffset, 0.01);
    }

    private static double MercatorY(double latitude)
    {
        double sine = Math.Sin(latitude * Math.PI / 180);
        return 0.5 - Math.Log((1 + sine) / (1 - sine)) / (4 * Math.PI);
    }
}
