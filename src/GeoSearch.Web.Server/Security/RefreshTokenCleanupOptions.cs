namespace GeoSearch.Web.Server.Security;

/// <summary>Controls periodic deletion of expired refresh-token history.</summary>
public sealed class RefreshTokenCleanupOptions
{
    public const string SECTION_NAME = "Auth:RefreshTokenCleanup";

    /// <summary>How long to keep expired refresh tokens for replay detection.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Time between background pruning attempts.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromDays(1);
}
