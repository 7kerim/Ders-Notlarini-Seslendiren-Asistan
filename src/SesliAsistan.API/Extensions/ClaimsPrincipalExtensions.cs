using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SesliAsistan.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Geçersiz oturum bilgisi.");
    }
}
