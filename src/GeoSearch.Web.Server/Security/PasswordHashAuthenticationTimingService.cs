using GeoSearch.Web.Server.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace GeoSearch.Web.Server.Security;

/// <summary>Performs a normal Identity password hash verification for unknown accounts.</summary>
internal sealed class PasswordHashAuthenticationTimingService : IAuthenticationTimingService
{
    private readonly GeoSearchUser _dummyUser = new() { Id = Guid.NewGuid().ToString(), UserName = "invalid-user" };
    private readonly PasswordHasher<GeoSearchUser> _passwordHasher;
    private readonly string _dummyPasswordHash;

    public PasswordHashAuthenticationTimingService(IOptions<PasswordHasherOptions> options)
    {
        _passwordHasher = new PasswordHasher<GeoSearchUser>(options);
        _dummyPasswordHash = _passwordHasher.HashPassword(_dummyUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public void PerformDummyPasswordVerification(string suppliedPassword) =>
        _ = _passwordHasher.VerifyHashedPassword(_dummyUser, _dummyPasswordHash, suppliedPassword);
}
