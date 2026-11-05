namespace GeoSearch.Web.Client.Search.Model;

/// <summary>Location request outcome. Only an explicit user action may initiate one.</summary>
public sealed record DeviceLocationResult(DeviceLocationFix? Fix, string? Error)
{
    public bool Succeeded => Fix is { IsValid: true };
}
