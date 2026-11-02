using System.ComponentModel.DataAnnotations;

namespace GeoSearch.Web.Shared.Auth;

public sealed record LoginRequest
(
    [Required, EmailAddress] string Email,
    [Required] string Password
);
