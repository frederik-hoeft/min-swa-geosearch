namespace GeoSearch.Web.Server.Security;

internal static class RefreshTokenCookiePolicy
{
    internal static CookieOptions Create(DateTimeOffset? expiresUtc = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expiresUtc,
        IsEssential = true,
    };
}
