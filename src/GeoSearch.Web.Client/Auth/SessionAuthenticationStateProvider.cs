using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace GeoSearch.Web.Client.Auth;

/// <summary>Client-side navigation state only; ASP.NET JWT middleware is the security boundary.</summary>
public sealed class SessionAuthenticationStateProvider(AuthSession session) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!session.IsAuthenticated || session.Email is null)
        {
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        }

        Claim[] claims =
        [
            new(ClaimTypes.Name, session.Email),
            new(ClaimTypes.Email, session.Email)
        ];
        ClaimsIdentity identity = new(claims, authenticationType: "Bearer");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    public void NotifySessionChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void Logout()
    {
        session.Clear();
        NotifySessionChanged();
    }
}
