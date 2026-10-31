using GeoSearch.Web.Shared.Auth;

namespace GeoSearch.Web.Client.Auth;

/// <summary>Ephemeral browser-session credentials. Never persisted to localStorage or cookies.</summary>
public sealed class AuthSession
{
    public string? AccessToken { get; private set; }
    public string? Email { get; private set; }
    public DateTimeOffset ExpiresUtc { get; private set; }
    public bool IsAuthenticated => AccessToken is not null && DateTimeOffset.UtcNow < ExpiresUtc;

    public void Set(LoginResponse response)
    {
        AccessToken = response.AccessToken;
        ExpiresUtc = response.ExpiresUtc;
        Email = response.Email;
    }

    public void Clear()
    {
        AccessToken = null;
        Email = null;
        ExpiresUtc = default;
    }
}
