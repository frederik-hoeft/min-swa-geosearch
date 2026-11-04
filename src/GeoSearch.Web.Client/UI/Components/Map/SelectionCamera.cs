using Community.Blazor.MapLibre.Models;

namespace GeoSearch.Web.Client.UI.Components.Map;

/// <summary>
/// Computes a camera center that displays a point above the mobile bottom sheet
/// without changing MapLibre's persistent camera padding.
/// </summary>
internal static class SelectionCamera
{
    private const double TILE_SIZE = 512;
    private const double MAX_MERCATOR_LATITUDE = 85.05112878;

    /// <summary>
    /// Shifts the geographic camera center south so the target appears higher
    /// in an unrotated, unpitched Web Mercator viewport. Coordinates are WGS84.
    /// </summary>
    public static LngLat GetCenter(LngLat target, double zoom, double upwardOffsetPixels)
    {
        if (upwardOffsetPixels <= 0)
        {
            return target;
        }

        double latitude = Math.Clamp(target.Latitude, -MAX_MERCATOR_LATITUDE, MAX_MERCATOR_LATITUDE);
        double radians = latitude * Math.PI / 180;
        double normalizedTargetY = (1 - Math.Log(Math.Tan(Math.PI / 4 + radians / 2)) / Math.PI) / 2;
        double worldSize = TILE_SIZE * Math.Pow(2, zoom);
        double normalizedCenterY = Math.Clamp(normalizedTargetY + upwardOffsetPixels / worldSize, 0, 1);
        double centerLatitude = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * normalizedCenterY))) * 180 / Math.PI;
        return new LngLat(target.Longitude, centerLatitude);
    }
}
