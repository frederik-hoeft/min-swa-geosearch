namespace GeoSearch.Web.Server.Data.RefreshTokens;

/// <summary>
/// Persists refresh-token state inside a transaction context owned by the calling authentication workflow.
/// Implementations may flush EF changes but must not create, commit, or roll back a transaction independently of the supplied context.
/// </summary>
internal interface IRefreshTokenDataAccess
{
    Task<RefreshTokenIssueResult> IssueAsync(IdentityDbContext context, string userId, string? securityStamp, CancellationToken cancellationToken = default);

    Task<RefreshTokenRotationResult?> RotateAsync(IdentityDbContext context, string token, CancellationToken cancellationToken = default);

    Task RevokeFamilyAsync(IdentityDbContext context, string token, CancellationToken cancellationToken = default);

    Task RevokeUserAsync(IdentityDbContext context, string userId, CancellationToken cancellationToken = default);
}
