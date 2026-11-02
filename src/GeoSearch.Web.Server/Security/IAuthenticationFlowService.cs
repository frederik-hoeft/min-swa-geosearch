using GeoSearch.Web.Shared.Auth;

namespace GeoSearch.Web.Server.Security;

/// <summary>Transactional authentication, refresh rotation, and revocation operations.</summary>
public interface IAuthenticationFlowService
{
    Task<AuthenticationTokenResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AuthenticationTokenResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<PasswordChangeResult?> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
