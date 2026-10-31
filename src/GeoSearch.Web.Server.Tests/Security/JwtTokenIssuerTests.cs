using System.IdentityModel.Tokens.Jwt;
using System.Text;
using GeoSearch.Web.Server.Security;
using GeoSearch.Web.Server.Data;
using Microsoft.IdentityModel.Tokens;

namespace GeoSearch.Web.Server.Tests.Security;

[TestClass]
public sealed class JwtTokenIssuerTests
{
    [TestMethod]
    public void IssuedTokenHasExpectedAudienceAndSignature()
    {
        JwtOptions options = new()
        {
            Issuer = "GeoSearch.Tests",
            Audience = "GeoSearch.TestClient",
            SigningKey = "completely-random-test-fixture-value-not-production"
        };
        JwtTokenIssuer issuer = new(options, TimeProvider.System);
        GeoSearchUser user = new() { Id = "user-123", Email = "student@example.test" };

        GeoSearch.Web.Shared.Auth.LoginResponse result = issuer.Issue(user);
        TokenValidationParameters validation = new()
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        JwtSecurityTokenHandler handler = new();
        System.Security.Claims.ClaimsPrincipal principal = handler.ValidateToken(result.AccessToken, validation, out SecurityToken validated);

        Assert.IsNotNull(principal);
        Assert.IsInstanceOfType<JwtSecurityToken>(validated);
        Assert.IsTrue(result.ExpiresUtc > DateTimeOffset.UtcNow);
    }
}
