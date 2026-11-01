using System.ComponentModel.DataAnnotations;

namespace GeoSearch.Web.Shared.Auth;

public sealed record ChangePasswordRequest(
    [property: Required] string CurrentPassword,
    [property: Required] string NewPassword);
