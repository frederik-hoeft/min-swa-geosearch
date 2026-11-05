namespace GeoSearch.Web.Client.Search.Model;

/// <summary>A WGS84 coordinate with explicit latitude/longitude names.</summary>
public readonly record struct GeoCoordinate(double Latitude, double Longitude)
{
    public bool IsValid => double.IsFinite(Latitude) && double.IsFinite(Longitude) &&
        Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180;
}
