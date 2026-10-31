using System.ComponentModel.DataAnnotations;

namespace GeoSearch.Web.Shared.Auth;

public sealed record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);
