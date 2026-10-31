using GeoSearch.Web.Server.Data;
using GeoSearch.Web.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace GeoSearch.Web.Server.Security;

/// <summary>
/// Coordinates authentication and durable Identity lockout state under the same request transaction.
/// Invalid passwords may update failed-attempt counters and therefore must commit those changes.
/// </summary>
internal sealed class AuthenticationFlowService(
    UserManager<GeoSearchUser> users,
    SignInManager<GeoSearchUser> signInManager,
    JwtTokenIssuer jwtTokenIssuer,
    ITransactionServiceHandle transactionService)
    : DatabaseService<IdentityDbContext>(transactionService), IAuthenticationFlowService
{
    public Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<LoginResponse?>(async (context, transaction, ct) =>
        {
            GeoSearchUser? user = await users.FindByEmailAsync(email).ConfigureAwait(false);
            if (user is null)
            {
                return transaction.Rollback<LoginResponse?>(null);
            }

            SignInResult result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                // Failed password attempts and lockout can persist through the Identity store.
                return transaction.Commit<LoginResponse?>(null);
            }

            LoginResponse token = jwtTokenIssuer.Issue(user);
            return transaction.Commit<LoginResponse?>(token);
        }, cancellationToken);
}
