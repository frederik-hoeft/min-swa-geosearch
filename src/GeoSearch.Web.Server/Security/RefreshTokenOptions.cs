namespace GeoSearch.Web.Server.Security;

/// <summary>Configuration for persistent, single-use browser refresh sessions.</summary>
public sealed class RefreshTokenOptions
{
    public const string SECTION_NAME = "Auth:RefreshToken";

    public string CookieName { get; set; } = "__Host-geosearch-refresh";

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(30);
}
