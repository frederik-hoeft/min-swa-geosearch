using GeoSearch.Web.Server.Data;
using GeoSearch.Web.Server.Data.RefreshTokens;
using GeoSearch.Web.Server.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace GeoSearch.Web.Server.Data.RefreshTokens;

internal sealed class RefreshTokenDataAccess(IOptions<RefreshTokenOptions> options, TimeProvider timeProvider) : IRefreshTokenDataAccess
{
    private readonly RefreshTokenOptions _options = options.Value;

    public async Task<RefreshTokenIssueResult> IssueAsync(
        IdentityDbContext context,
        string userId,
        string? securityStamp,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        DateTimeOffset now = timeProvider.GetUtcNow();
        string token = GenerateToken();
        DateTimeOffset expiresAt = now.Add(_options.Lifetime);

        context.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(token),
            FamilyId = Guid.CreateVersion7(),
            SecurityStamp = securityStamp,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        });

        await context.SaveChangesAsync(cancellationToken);
        return new RefreshTokenIssueResult(token, expiresAt);
    }

    public async Task<RefreshTokenRotationResult?> RotateAsync(IdentityDbContext context, string token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        string tokenHash = HashToken(token);
        DateTimeOffset now = timeProvider.GetUtcNow();
        RefreshToken? current = await context.Set<RefreshToken>()
            .Include(static refreshToken => refreshToken.User)
            .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);

        if (current is null)
        {
            return null;
        }

        if (!string.Equals(current.SecurityStamp, current.User.SecurityStamp, StringComparison.Ordinal)
            || current.RevokedAt is not null
            || current.ExpiresAt <= now)
        {
            // Invalidating the whole active family is deliberate. It keeps replay, expiry,
            // and changed-identity state conservative and makes this path robust when a
            // sibling request is rotating the same family concurrently.
            context.ChangeTracker.Clear();
            await RevokeActiveFamilyTokensBulkAsync(context, current.FamilyId, now, cancellationToken);
            return null;
        }

        string replacementToken = GenerateToken();
        string replacementHash = HashToken(replacementToken);
        DateTimeOffset replacementExpiresAt = now.Add(_options.Lifetime);

        current.RevokedAt = now;
        current.ReplacedByTokenHash = replacementHash;
        current.ConcurrencyToken = Guid.NewGuid().ToString("N");

        context.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = current.UserId,
            TokenHash = replacementHash,
            FamilyId = current.FamilyId,
            SecurityStamp = current.SecurityStamp,
            CreatedAt = now,
            ExpiresAt = replacementExpiresAt,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request consumed this token after it was read. Clear stale tracked
            // state, then invalidate whatever token is now active in the family. With the
            // request transaction at ReadCommitted this update observes the winning commit.
            context.ChangeTracker.Clear();
            await RevokeActiveFamilyTokensBulkAsync(context, current.FamilyId, now, cancellationToken);
            return null;
        }

        return new RefreshTokenRotationResult(current.UserId, replacementToken, replacementExpiresAt);
    }

    public async Task RevokeFamilyAsync(IdentityDbContext context, string token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        string tokenHash = HashToken(token);
        RefreshToken? current = await context.Set<RefreshToken>()
            .AsNoTracking()
            .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);
        if (current is null)
        {
            // Revocation is idempotent. A missing token is a successful no-op.
            return;
        }

        await RevokeActiveFamilyTokensBulkAsync(context, current.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
    }

    public async Task RevokeUserAsync(IdentityDbContext context, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        DateTimeOffset revokedAt = timeProvider.GetUtcNow();
        string concurrencyToken = Guid.NewGuid().ToString("N");
        await context.Set<RefreshToken>()
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, (DateTimeOffset?)revokedAt)
                .SetProperty(token => token.ConcurrencyToken, concurrencyToken), cancellationToken);
    }

    private static async Task RevokeActiveFamilyTokensBulkAsync(
        IdentityDbContext context,
        Guid familyId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        string concurrencyToken = Guid.NewGuid().ToString("N");
        await context.Set<RefreshToken>()
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, (DateTimeOffset?)revokedAt)
                .SetProperty(token => token.ConcurrencyToken, concurrencyToken), cancellationToken);
    }

    private static string GenerateToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
