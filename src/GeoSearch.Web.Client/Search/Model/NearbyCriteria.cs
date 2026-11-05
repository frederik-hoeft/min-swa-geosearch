namespace GeoSearch.Web.Client.Search.Model;

/// <summary>Editable nearby criteria; no coordinate is captured until submission.</summary>
public sealed record NearbyCriteria(string? Category = null, int RadiusMeters = 500,
    NearbyOriginKind OriginKind = NearbyOriginKind.MapCenter, GeoCoordinate? SelectedPoint = null)
{
    public static IReadOnlyList<int> Radii { get; } = [250, 500, 1000, 2000];

    public bool IsValid => Radii.Contains(RadiusMeters) &&
        (Category is null || (Category.Length is > 0 and <= 200 && !string.IsNullOrWhiteSpace(Category))) &&
        (OriginKind != NearbyOriginKind.SelectedPoint || SelectedPoint is { IsValid: true });
}
