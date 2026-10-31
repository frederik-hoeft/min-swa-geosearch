using GeoSearch.Web.Shared.Auth;

namespace GeoSearch.Web.Server.Security;

/// <summary>Owns authentication workflow and persistent Identity changes.</summary>
public interface IAuthenticationFlowService
{
    Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}
