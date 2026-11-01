using FluentValidation;
using Microsoft.EntityFrameworkCore;
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
    private readonly IPasswordResetCodeRepository _resetCodes;
    private readonly IEmailSender _emailSender;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotPasswordValidator;
    private readonly IValidator<ResetPasswordRequest> _resetPasswordValidator;
    private readonly JwtOptions _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordResetCodeRepository resetCodes,
        IEmailSender emailSender,
        IUnitOfWork uow,
        IPasswordHasher hasher,
        ITokenService tokens,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<ForgotPasswordRequest> forgotPasswordValidator,
        IValidator<ResetPasswordRequest> resetPasswordValidator,
        IOptions<JwtOptions> jwt,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _resetCodes = resetCodes;
        _emailSender = emailSender;
        _uow = uow;
        _hasher = hasher;
        _tokens = tokens;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _forgotPasswordValidator = forgotPasswordValidator;
        _resetPasswordValidator = resetPasswordValidator;
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

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        await _forgotPasswordValidator.ValidateOrThrowAsync(request, ct);

        var email = NormalizeEmail(request.Email);
        var user = await _users.GetByEmailAsync(email, ct);

        // Kullanıcı bulunamazsa da bilgi sızdırmamak için hata fırlatılmaz
        if (user is null || user.IsDeleted || !user.IsActive)
            return;

        // Önceki aktif kodları iptal et
        await _resetCodes.InvalidateUserCodesAsync(user.Id, ct);

        // 6 haneli rastgele kod üret
        var plainCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var codeHash = _tokens.HashToken(plainCode);

        var resetCode = new PasswordResetCode
        {
            UserId = user.Id,
            CodeHash = codeHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            FailedAttempts = 0,
            IsUsed = false
        };

        await _resetCodes.AddAsync(resetCode, ct);
        await _uow.SaveChangesAsync(ct);

        await _emailSender.SendPasswordResetCodeAsync(user.Email, plainCode, ct);
        _logger.LogInformation("Kullanıcı için şifre sıfırlama kodu oluşturuldu: {UserId}", user.Id);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        await _resetPasswordValidator.ValidateOrThrowAsync(request, ct);

        var email = NormalizeEmail(request.Email);
        var user = await _users.GetByEmailAsync(email, ct);

        if (user is null || user.IsDeleted || !user.IsActive)
            throw new UnauthorizedAppException("Geçersiz veya süresi dolmuş sıfırlama kodu.");

        var validCode = await _resetCodes.GetLatestValidCodeAsync(user.Id, ct);
        if (validCode is null || !validCode.IsValid)
            throw new UnauthorizedAppException("Geçersiz veya süresi dolmuş sıfırlama kodu.");

        var inputHash = _tokens.HashToken(request.Code);
        if (inputHash != validCode.CodeHash)
        {
            validCode.FailedAttempts++;
            if (validCode.FailedAttempts >= 5)
            {
                validCode.IsUsed = true;
            }
            await _uow.SaveChangesAsync(ct);
            throw new UnauthorizedAppException("Geçersiz veya süresi dolmuş sıfırlama kodu.");
        }

        // Kod doğru: şifreyi güncelle ve kodu kullanıldı yap
        validCode.IsUsed = true;
        user.PasswordHash = _hasher.Hash(request.NewPassword);

        // Tüm açık oturumları (refresh token'ları) kapat
        var activeTokens = await _refreshTokens.Query()
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var t in activeTokens)
        {
            t.RevokedAt = DateTime.UtcNow;
        }

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Şifre sıfırlandı ve tüm oturumlar kapatıldı: {UserId}", user.Id);
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
