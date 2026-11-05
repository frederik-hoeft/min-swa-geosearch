namespace GeoSearch.Web.Client.UI.Components.Map;

/// <summary>Approximate projected diameter of a metre-radius circle in Web Mercator.</summary>
internal static class MapCircleScale
{
    private const double METERS_PER_PIXEL_AT_EQUATOR = 156543.03392804097;

    public static double DiameterPixels(double radiusMeters, double latitude, double zoom)
    {
        if (!double.IsFinite(radiusMeters) || radiusMeters <= 0 || !double.IsFinite(latitude) || !double.IsFinite(zoom))
        {
            return 0;
        }

        double cosine = Math.Cos(Math.Clamp(latitude, -85.05112878, 85.05112878) * Math.PI / 180);
        double metersPerPixel = METERS_PER_PIXEL_AT_EQUATOR * cosine / Math.Pow(2, zoom);
        return Math.Clamp(2 * radiusMeters / metersPerPixel, 0, 5000);
    }
}
