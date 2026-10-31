using GeoSearch.Web.Server.Security;
using GeoSearch.Web.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace GeoSearch.Web.Server.Api.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signInManager,
    JwtTokenIssuer jwtTokenIssuer) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> LoginAsync([FromBody] LoginRequest request)
    {
        IdentityUser? user = await users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized(new ApiError("Invalid email or password."));
        }
        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return Unauthorized(new ApiError("Invalid email or password."));
        }
        return Ok(jwtTokenIssuer.Issue(user));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    public ActionResult<AccountResponse> Me()
    {
        string email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        return Ok(new AccountResponse(email));
    }
}
