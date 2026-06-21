using Construcheck.API.Extensions;
using Construcheck.API.Modules.Auth.DTOs;
using Construcheck.API.Modules.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

namespace Construcheck.API.Modules.Auth.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request, ct);

        if (result.IsFailure)
            return result.ToActionResult(this);

        SetRefreshTokenCookie(result.Value!.RefreshToken);

        return Ok(new { accessToken = result.Value.AccessToken });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);

        if (result.IsFailure)
            return result.ToActionResult(this);

        SetRefreshTokenCookie(result.Value!.RefreshToken);

        return Ok(new { accessToken = result.Value.AccessToken });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var token = Request.Cookies[RefreshTokenCookieName];

        if (string.IsNullOrEmpty(token))
            return Unauthorized(new { error = "Refresh token ausente." });

        var result = await authService.RefreshAsync(token, ct);

        if (result.IsFailure)
            return result.ToActionResult(this);

        SetRefreshTokenCookie(result.Value!.RefreshToken);

        return Ok(new { accessToken = result.Value.AccessToken });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var token = Request.Cookies[RefreshTokenCookieName];

        if (!string.IsNullOrEmpty(token))
            await authService.LogoutAsync(token, ct);

        Response.Cookies.Delete(RefreshTokenCookieName);

        return NoContent();
    }

    [HttpPut("users/{id:guid}/roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateUserRoles(
        Guid id,
        [FromBody] UpdateUserRolesRequest request,
        CancellationToken ct)
    {
        var result = await authService.UpdateUserRolesAsync(id, request, ct);
        return result.ToActionResult(this);
    }

    private void SetRefreshTokenCookie(string token)
    {
        Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(
                int.Parse(HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>()["REFRESH_TOKEN_EXPIRATION_DAYS"]!))
        });
    }
}