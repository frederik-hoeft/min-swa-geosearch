using GeoSearch.Web.Server.Data;
using GeoSearch.Web.Server.Data.RefreshTokens;
using GeoSearch.Web.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace GeoSearch.Web.Server.Security;

/// <summary>Owns all durable authentication session changes in a WKG request transaction.</summary>
internal sealed class AuthenticationFlowService(
    UserManager<GeoSearchUser> users,
    SignInManager<GeoSearchUser> signInManager,
    JwtTokenIssuer jwtTokenIssuer,
    IAuthenticationTimingService timing,
    IRefreshTokenDataAccess refreshTokens,
    ITransactionServiceHandle transactionService)
    : DatabaseService<IdentityDbContext>(transactionService), IAuthenticationFlowService
{
    public Task<AuthenticationTokenResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<AuthenticationTokenResult?>(async (context, transaction, ct) =>
        {
            GeoSearchUser? user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                timing.PerformDummyPasswordVerification(password);
                return transaction.Rollback<AuthenticationTokenResult?>(null);
            }

            SignInResult result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut || result.IsNotAllowed)
                {
                    timing.PerformDummyPasswordVerification(password);
                }

                // Identity may update lockout counters even for a failed login.
                return transaction.Commit<AuthenticationTokenResult?>(null);
            }

            LoginResponse accessToken = jwtTokenIssuer.Issue(user);
            RefreshTokenIssueResult refresh = await refreshTokens.IssueAsync(context, user.Id, user.SecurityStamp, ct);
            return transaction.Commit<AuthenticationTokenResult?>(new(accessToken, refresh.Token, refresh.ExpiresAt));
        }, cancellationToken);

    public Task<AuthenticationTokenResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<AuthenticationTokenResult?>(async (context, transaction, ct) =>
        {
            RefreshTokenRotationResult? rotation = await refreshTokens.RotateAsync(context, refreshToken, ct);
            if (rotation is null)
            {
                // Invalid or replayed tokens may revoke their entire family, so commit even on 401.
                return transaction.Commit<AuthenticationTokenResult?>(null);
            }

            GeoSearchUser? user = await users.FindByIdAsync(rotation.UserId);
            if (user is null)
            {
                await refreshTokens.RevokeFamilyAsync(context, rotation.Token, ct);
                return transaction.Commit<AuthenticationTokenResult?>(null);
            }

            bool canSignIn = await signInManager.CanSignInAsync(user);
            bool lockedOut = users.SupportsUserLockout && await users.IsLockedOutAsync(user);
            if (!canSignIn || lockedOut)
            {
                await refreshTokens.RevokeFamilyAsync(context, rotation.Token, ct);
                return transaction.Commit<AuthenticationTokenResult?>(null);
            }

            LoginResponse accessToken = jwtTokenIssuer.Issue(user);
            return transaction.Commit<AuthenticationTokenResult?>(new(accessToken, rotation.Token, rotation.ExpiresAt));
        }, cancellationToken);

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        bool committed = await Transaction.Scoped.RunAsync(async (context, transaction, ct) =>
        {
            await refreshTokens.RevokeFamilyAsync(context, refreshToken, ct);
            return transaction.Commit(true);
        }, cancellationToken);
    }

    public Task<PasswordChangeResult?> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<PasswordChangeResult?>(async (context, transaction, ct) =>
        {
            GeoSearchUser? user = await users.FindByIdAsync(userId);
            if (user is null)
            {
                return transaction.Rollback<PasswordChangeResult?>(null);
            }

            IdentityResult changed = await users.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!changed.Succeeded)
            {
                return transaction.Rollback<PasswordChangeResult?>(new(false, null));
            }

            await refreshTokens.RevokeUserAsync(context, user.Id, ct);
            LoginResponse accessToken = jwtTokenIssuer.Issue(user);
            RefreshTokenIssueResult refresh = await refreshTokens.IssueAsync(context, user.Id, user.SecurityStamp, ct);
            return transaction.Commit<PasswordChangeResult?>(new(true, new(accessToken, refresh.Token, refresh.ExpiresAt)));
        }, cancellationToken);
}
