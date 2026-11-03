using GeoSearch.Web.Server.Security;
using GeoSearch.Web.Shared.Api;
using GeoSearch.Web.Shared.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GeoSearch.Web.Server.Api.Auth;

[ApiController]
[Route("api/auth")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AuthController(
    IAuthenticationFlowService authentication,
    IAntiforgery antiforgery,
    IOptions<RefreshTokenOptions> refreshOptions) : ControllerBase
{
    private readonly RefreshTokenOptions _refreshOptions = refreshOptions.Value;

    [HttpGet("antiforgery")]
    [AllowAnonymous]
    public ActionResult<AntiforgeryResponse> GetAntiforgeryToken()
    {
        AntiforgeryTokenSet tokenSet = antiforgery.GetAndStoreTokens(HttpContext);
        string token = tokenSet.RequestToken
            ?? throw new InvalidOperationException("Antiforgery token generation failed.");
        return Ok(new AntiforgeryResponse(token));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        AuthenticationTokenResult? result = await authentication.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result is null)
        {
            return Unauthorized(new ApiError("Invalid email or password."));
        }

        SetRefreshCookie(result);
        return Ok(result.AccessToken);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [RequireAntiforgeryValidation]
    public async Task<ActionResult<LoginResponse>> RefreshAsync(CancellationToken cancellationToken)
    {
        if (!TryGetRefreshToken(out string? refreshToken))
        {
            return Unauthorized();
        }

        AuthenticationTokenResult? result = await authentication.RefreshAsync(refreshToken!, cancellationToken);
        if (result is null)
        {
            DeleteRefreshCookie();
            return Unauthorized();
        }

        SetRefreshCookie(result);
        return Ok(result.AccessToken);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [RequireAntiforgeryValidation]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        if (TryGetRefreshToken(out string? refreshToken))
        {
            await authentication.RevokeAsync(refreshToken!, cancellationToken);
        }

        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPost("password")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> ChangePasswordAsync([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        string? userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        PasswordChangeResult? result = await authentication.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
        if (result is null)
        {
            DeleteRefreshCookie();
            return Unauthorized();
        }
        if (!result.Succeeded)
        {
            return BadRequest(new ApiError("Password change rejected."));
        }

        AuthenticationTokenResult replacement = result.Authentication
            ?? throw new InvalidOperationException("A successful password change must issue a replacement session.");
        SetRefreshCookie(replacement);
        return Ok(replacement.AccessToken);
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<AccountResponse> Me()
    {
        string email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        return Ok(new AccountResponse(email));
    }

    private bool TryGetRefreshToken(out string? token) =>
        Request.Cookies.TryGetValue(_refreshOptions.CookieName, out token) && !string.IsNullOrWhiteSpace(token);

    private void SetRefreshCookie(AuthenticationTokenResult result) => Response.Cookies.Append(
        _refreshOptions.CookieName, result.RefreshToken,
        RefreshTokenCookiePolicy.Create(result.RefreshExpiresUtc));

    private void DeleteRefreshCookie() => Response.Cookies.Delete(
        _refreshOptions.CookieName, RefreshTokenCookiePolicy.Create());
}
