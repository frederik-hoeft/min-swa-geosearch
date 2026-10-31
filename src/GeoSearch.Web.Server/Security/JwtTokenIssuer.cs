using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GeoSearch.Web.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace GeoSearch.Web.Server.Security;

public sealed class JwtTokenIssuer(JwtOptions options, TimeProvider timeProvider)
{
    private static readonly TimeSpan s_tokenLifetime = TimeSpan.FromMinutes(20);

    public LoginResponse Issue(IdentityUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        string email = user.Email ?? throw new InvalidOperationException("Identity email is missing.");
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expiration = now.Add(s_tokenLifetime);
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        ];
        JwtSecurityToken token = new(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiration.UtcDateTime,
            signingCredentials: credentials);
        string serialized = new JwtSecurityTokenHandler().WriteToken(token);
        return new LoginResponse(serialized, expiration, email);
    }
}
