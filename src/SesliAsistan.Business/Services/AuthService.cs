using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SesliAsistan.Business.DTOs.Auth;
using SesliAsistan.Business.Exceptions;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.Business.Options;
using SesliAsistan.Business.Validation;
using SesliAsistan.DataAccess.Repositories;
using SesliAsistan.Entities.Entities;
using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Business.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly JwtOptions _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork uow,
        IPasswordHasher hasher,
        ITokenService tokens,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IOptions<JwtOptions> jwt,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _uow = uow;
        _hasher = hasher;
        _tokens = tokens;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _jwt = jwt.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken ct = default)
    {
        await _registerValidator.ValidateOrThrowAsync(request, ct);

        var email = NormalizeEmail(request.Email);
        if (await _users.EmailExistsAsync(email, ct))
            throw new ConflictException("Bu e-posta adresiyle kayıtlı bir hesap zaten var.");

        var user = new User
        {
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            Role = UserRole.Student
        };
        await _users.AddAsync(user, ct);

        var response = await IssueTokensAsync(user, ipAddress, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Yeni kullanıcı kaydı: {UserId}", user.Id);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default)
    {
        await _loginValidator.ValidateOrThrowAsync(request, ct);

        var user = await _users.GetByEmailAsync(NormalizeEmail(request.Email), ct);

        // Kullanıcı yoksa da aynı mesaj: hesap varlığı dışarı sızdırılmaz (user enumeration önlemi)
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAppException("E-posta veya şifre hatalı.");

        if (!user.IsActive)
            throw new ForbiddenAppException("Hesabınız askıya alınmıştır. Lütfen yönetici ile iletişime geçin.");

        user.LastLoginAt = DateTime.UtcNow;
        var response = await IssueTokensAsync(user, ipAddress, ct);
        await _uow.SaveChangesAsync(ct);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await _refreshTokens.GetByHashAsync(_tokens.HashToken(refreshToken), ct);
        if (existing is null || !existing.IsActive || existing.User.IsDeleted || !existing.User.IsActive)
            throw new UnauthorizedAppException("Oturum süresi doldu. Lütfen tekrar giriş yapın.");

        // Token rotasyonu: eski token iptal edilir, yerine yenisi verilir
        var response = await IssueTokensAsync(existing.User, ipAddress, ct);
        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedByTokenHash = _tokens.HashToken(response.RefreshToken);

        await _uow.SaveChangesAsync(ct);
        return response;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await _refreshTokens.GetByHashAsync(_tokens.HashToken(refreshToken), ct);
        if (existing is null || existing.RevokedAt is not null) return;

        existing.RevokedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, string? ipAddress, CancellationToken ct)
    {
        var (accessToken, expiresAt) = _tokens.CreateAccessToken(user);
        var (plainRefresh, refreshHash) = _tokens.CreateRefreshToken();

        await _refreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays),
            CreatedByIp = ipAddress
        }, ct);

        return new AuthResponse(accessToken, expiresAt, plainRefresh, user.ToDto());
    }

    internal static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

internal static class UserMappings
{
    public static UserDto ToDto(this User u) =>
        new(u.Id, u.Email, u.FullName, u.Role.ToString(), u.PreferredVoice, u.PlaybackSpeed, u.CreatedAt);
}
