namespace GeoSearch.Web.Shared.Auth;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresUtc, string Email);
