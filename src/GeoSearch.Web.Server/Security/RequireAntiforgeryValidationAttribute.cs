namespace GeoSearch.Web.Server.Security;

/// <summary>Marks a cookie-authenticated endpoint that requires an antiforgery request token.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class RequireAntiforgeryValidationAttribute : Attribute;
