using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SesliAsistan.API.Extensions;
using SesliAsistan.Business.DTOs.Auth;
using SesliAsistan.Business.Interfaces;

namespace SesliAsistan.API.Controllers;

/// <summary>FG-1: Kayıt, giriş, oturum yenileme ve çıkış.</summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[EnableRateLimiting(ServiceCollectionExtensions.AuthRateLimitPolicy)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Yeni öğrenci hesabı oluşturur ve oturum açar.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(request, ClientIp, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>E-posta ve şifre ile giriş yapar; erişim ve yenileme anahtarı döner.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(request, ClientIp, ct));

    /// <summary>Süresi dolan erişim anahtarını yeniler (token rotasyonu).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request, CancellationToken ct) =>
        Ok(await _auth.RefreshAsync(request.RefreshToken, ClientIp, ct));

    /// <summary>Yenileme anahtarını iptal ederek oturumu kapatır.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
