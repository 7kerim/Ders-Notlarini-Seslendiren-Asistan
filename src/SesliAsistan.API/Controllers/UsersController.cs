using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SesliAsistan.API.Extensions;
using SesliAsistan.Business.DTOs.Auth;
using SesliAsistan.Business.Interfaces;

namespace SesliAsistan.API.Controllers;

/// <summary>FG-1: Oturum açmış kullanıcının profil işlemleri.</summary>
[ApiController]
[Route("api/v1/users/me")]
[Authorize(Policy = "StudentOrAdmin")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    [HttpGet]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Get(CancellationToken ct) =>
        Ok(await _users.GetProfileAsync(User.GetUserId(), ct));

    /// <summary>Ad soyad ile varsayılan ses ve oynatma hızı tercihlerini günceller.</summary>
    [HttpPut]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> Update(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await _users.UpdateProfileAsync(User.GetUserId(), request, ct));

    /// <summary>Şifre değiştirir; tüm açık oturumlar sonlandırılır.</summary>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await _users.ChangePasswordAsync(User.GetUserId(), request, ct);
        return NoContent();
    }

    /// <summary>KVKK kapsamında hesabı siler.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        await _users.DeleteAccountAsync(User.GetUserId(), ct);
        return NoContent();
    }
}
