using System.ComponentModel.DataAnnotations;

namespace GeoSearch.Web.Shared.Auth;

public sealed record ChangePasswordRequest
(
    [Required] string CurrentPassword,
    [Required] string NewPassword
);
