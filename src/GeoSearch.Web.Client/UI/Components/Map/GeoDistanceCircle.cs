using Community.Blazor.MapLibre.Models.Feature;
using GeoSearch.Web.Client.Search.Model;

namespace GeoSearch.Web.Client.UI.Components.Map;

/// <summary>
/// Creates a WGS84 coordinate ring around a fixed geographic origin. MapLibre
/// projects the resulting GeoJSON polygon at every zoom; no pixel geometry is
/// calculated by the application.
/// </summary>
internal static class GeoDistanceCircle
{
    private const int SEGMENTS = 96;
    private const double EARTH_RADIUS_METERS = 6371008.8;

    public static FeatureCollection Create(GeoCoordinate origin, double radiusMeters)
    {
        if (!origin.IsValid || !double.IsFinite(radiusMeters) || radiusMeters <= 0)
        {
            return new FeatureCollection();
        }

        double latitude = DegreesToRadians(origin.Latitude);
        double longitude = DegreesToRadians(origin.Longitude);
        double distance = radiusMeters / EARTH_RADIUS_METERS;
        double sinLatitude = Math.Sin(latitude);
        double cosLatitude = Math.Cos(latitude);
        double sinDistance = Math.Sin(distance);
        double cosDistance = Math.Cos(distance);
        double[][] ring = new double[SEGMENTS + 1][];

        for (int i = 0; i < SEGMENTS; ++i)
        {
            // GeoJSON exterior rings use counterclockwise winding. Start north and turn west.
            double bearing = -2 * Math.PI * i / SEGMENTS;
            double nextLatitude = Math.Asin(Math.Clamp(sinLatitude * cosDistance +
                cosLatitude * sinDistance * Math.Cos(bearing), -1, 1));
            double nextLongitude = longitude + Math.Atan2(
                Math.Sin(bearing) * sinDistance * cosLatitude,
                cosDistance - sinLatitude * Math.Sin(nextLatitude));

            // Keep nearby longitudes continuous when the ring crosses ±180°;
            // MapLibre's GeoJSON renderer handles longitude wrapping.
            ring[i] = [RadiansToDegrees(nextLongitude), RadiansToDegrees(nextLatitude)];
        }

        ring[SEGMENTS] = [.. ring[0]];
        return new FeatureCollection
        {
            Features = [new FeatureFeature
            {
                Geometry = new PolygonGeometry { Coordinates = [ring] }
            }]
        };
    }

    private static double DegreesToRadians(double angle) => angle * Math.PI / 180;

    private static double RadiansToDegrees(double angle) => angle * 180 / Math.PI;
}
