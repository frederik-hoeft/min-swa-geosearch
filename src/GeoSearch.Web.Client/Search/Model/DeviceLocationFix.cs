namespace GeoSearch.Web.Client.Search.Model;

/// <summary>Browser-reported location, including its independent accuracy radius.</summary>
public sealed record DeviceLocationFix(GeoCoordinate Point, double AccuracyMeters)
{
    public bool IsValid => Point.IsValid && double.IsFinite(AccuracyMeters) && AccuracyMeters >= 0;
}
