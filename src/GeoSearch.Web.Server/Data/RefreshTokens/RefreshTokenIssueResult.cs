namespace GeoSearch.Web.Server.Data.RefreshTokens;

internal sealed record RefreshTokenIssueResult(string Token, DateTimeOffset ExpiresAt);
