using System.Text;
using Microsoft.Extensions.Configuration;

namespace GeoSearch.Web.Server.Security;

public sealed class JwtOptions
{
    public const string SECTION = "Jwt";

    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }

    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        string issuer = configuration["Issuer"] ?? string.Empty;
        string audience = configuration["Audience"] ?? string.Empty;
        string signingKey = configuration["SigningKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience)
            || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "JWT issuer, audience, and random signing key (at least 32 bytes) are required.");
        }
        return new JwtOptions { Issuer = issuer, Audience = audience, SigningKey = signingKey };
    }
}
