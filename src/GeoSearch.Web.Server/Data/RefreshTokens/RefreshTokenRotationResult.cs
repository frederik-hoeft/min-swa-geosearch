namespace GeoSearch.Web.Server.Data.RefreshTokens;

internal sealed record RefreshTokenRotationResult(string UserId, string Token, DateTimeOffset ExpiresAt);
