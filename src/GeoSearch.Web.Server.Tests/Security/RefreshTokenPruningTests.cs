using GeoSearch.Web.Server.Data.RefreshTokens;

namespace GeoSearch.Web.Server.Tests.Security;

[TestClass]
public sealed class RefreshTokenPruningTests
{
    [TestMethod]
    public void PruningPreservesTokensUntilExpiryPlusRetention()
    {
        DateTimeOffset now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset cutoff = now.AddDays(-30);
        List<RefreshToken> records =
        [
            CreateToken(1, now.AddDays(-31), now.AddDays(-40)),
            CreateToken(2, cutoff, now.AddDays(-32)),
            CreateToken(3, now.AddDays(-1), now.AddDays(-20)),
            CreateToken(4, now.AddDays(1), null),
        ];

        long[] expiredIds = [.. RefreshTokenPruningService.SelectExpiredHistory(records.AsQueryable(), cutoff)
            .Select(token => token.Id)];

        CollectionAssert.AreEqual(new long[] { 1 }, expiredIds);
    }

    private static RefreshToken CreateToken(long id, DateTimeOffset expiresAt, DateTimeOffset? revokedAt) => new()
    {
        Id = id,
        UserId = "test-user",
        TokenHash = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        FamilyId = Guid.Empty,
        ExpiresAt = expiresAt,
        RevokedAt = revokedAt,
    };
}
