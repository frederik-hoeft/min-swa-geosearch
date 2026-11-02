using GeoSearch.Web.Client.Api;
using GeoSearch.Web.Shared.Auth;

namespace GeoSearch.Web.Client.Auth;

/// <summary>Manages short-lived access tokens while the server owns the cookie session.</summary>
public sealed class AuthenticationService(
    AuthApiClient api,
    AuthSession session,
    SessionAuthenticationStateProvider state,
    BrowserAuthCoordinator coordinator,
    TimeProvider clock)
{
    private static readonly TimeSpan s_refreshLeadTime = TimeSpan.FromSeconds(30);

    public Task InitializeAsync(CancellationToken cancellationToken = default) => RefreshAsync(null, cancellationToken);

    public Task<bool> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        coordinator.RunAsync(async ct =>
        {
            LoginResponse? response = await api.LoginAsync(new LoginRequest(email, password), ct);
            if (response is null)
            {
                return false;
            }

            session.Set(response);
            state.NotifySessionChanged();
            return true;
        }, cancellationToken);

    public Task LogoutAsync(CancellationToken cancellationToken = default) =>
        coordinator.RunAsync(async ct =>
        {
            // A failed revocation must not be disguised as successful server-side logout.
            await api.LogoutAsync(ct);
            state.Logout();
            return true;
        }, cancellationToken);

    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (session.AccessToken is { } token && session.ExpiresUtc > clock.GetUtcNow().Add(s_refreshLeadTime))
        {
            return Task.FromResult<string?>(token);
        }

        return RefreshAsync(null, cancellationToken);
    }

    public Task<string?> RefreshAfterUnauthorizedAsync(string rejectedToken, CancellationToken cancellationToken = default) =>
        RefreshAsync(rejectedToken, cancellationToken);

    public Task<bool> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default) =>
        coordinator.RunAsync(async ct =>
        {
            string? token = await EnsureRefreshedWithinLockAsync(null, ct);
            if (token is null)
            {
                return false;
            }

            LoginResponse? replacement = await api.ChangePasswordAsync(new(currentPassword, newPassword), token, ct);
            if (replacement is null)
            {
                return false;
            }

            session.Set(replacement);
            state.NotifySessionChanged();
            return true;
        }, cancellationToken);

    private Task<string?> RefreshAsync(string? rejectedToken, CancellationToken cancellationToken) =>
        coordinator.RunAsync(ct => EnsureRefreshedWithinLockAsync(rejectedToken, ct), cancellationToken);

    private async Task<string?> EnsureRefreshedWithinLockAsync(string? rejectedToken, CancellationToken cancellationToken)
    {
        if (session.AccessToken is { } token)
        {
            bool valid = session.ExpiresUtc > clock.GetUtcNow().Add(rejectedToken is null ? s_refreshLeadTime : TimeSpan.Zero);
            bool different = rejectedToken is not null && !string.Equals(token, rejectedToken, StringComparison.Ordinal);
            if (valid && (rejectedToken is null || different))
            {
                return token;
            }
        }

        LoginResponse? response = await api.TryRefreshAsync(cancellationToken);
        if (response is null)
        {
            state.Logout();
            return null;
        }

        session.Set(response);
        state.NotifySessionChanged();
        return response.AccessToken;
    }
}
