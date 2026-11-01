namespace GeoSearch.Web.Server.Security;

/// <summary>Expected credential error or replacement authentication session.</summary>
public sealed record PasswordChangeResult(bool Succeeded, AuthenticationTokenResult? Authentication);
