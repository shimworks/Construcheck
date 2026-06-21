using Construcheck.API.Extensions;
using Construcheck.API.Modules.Auth.DTOs;
using Construcheck.API.Modules.Auth.Interfaces;
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

    // Refresh e Logout — adicionados no Tópico 5
    // UpdateUserRoles — adicionado no Tópico 6

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