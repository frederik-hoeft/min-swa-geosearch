using GeoSearch.Web.Shared.Auth;

namespace GeoSearch.Web.Server.Security;

/// <summary>Access-token response and server-only refresh-token material.</summary>
public sealed record AuthenticationTokenResult(LoginResponse AccessToken, string RefreshToken, DateTimeOffset RefreshExpiresUtc);
