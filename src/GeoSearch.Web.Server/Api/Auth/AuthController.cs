using System.Security.Claims;
using GeoSearch.Web.Server.Security;
using GeoSearch.Web.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GeoSearch.Web.Server.Api.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthenticationFlowService authentication) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        LoginResponse? result = await authentication.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result is null)
        {
            return Unauthorized(new ApiError("Invalid email or password."));
        }

        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    public ActionResult<AccountResponse> Me()
    {
        Response.Headers.CacheControl = "no-store";
        string email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        return Ok(new AccountResponse(email));
    }
}
