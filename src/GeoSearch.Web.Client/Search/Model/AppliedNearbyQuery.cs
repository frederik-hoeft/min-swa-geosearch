namespace GeoSearch.Web.Client.Search.Model;

/// <summary>Immutable criteria and WGS84 origin captured at submission time.</summary>
public sealed record AppliedNearbyQuery(NearbyCriteria Criteria, GeoCoordinate Origin)
{
    public bool IsValid => Criteria.IsValid && Origin.IsValid;
}
