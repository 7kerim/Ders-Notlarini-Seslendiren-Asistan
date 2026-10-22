using FluentValidation;
using SesliAsistan.Business.DTOs.Auth;
using SesliAsistan.Business.Exceptions;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.Business.Validation;
using SesliAsistan.DataAccess.Repositories;
using SesliAsistan.Entities.Entities;

namespace SesliAsistan.Business.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IGenericRepository<RefreshToken> _refreshTokens;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IValidator<UpdateProfileRequest> _profileValidator;
    private readonly IValidator<ChangePasswordRequest> _passwordValidator;

    public UserService(
        IUserRepository users,
        IGenericRepository<RefreshToken> refreshTokens,
        IUnitOfWork uow,
        IPasswordHasher hasher,
        IValidator<UpdateProfileRequest> profileValidator,
        IValidator<ChangePasswordRequest> passwordValidator)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _uow = uow;
        _hasher = hasher;
        _profileValidator = profileValidator;
        _passwordValidator = passwordValidator;
    }

    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct = default) =>
        (await GetUserAsync(userId, ct)).ToDto();

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        await _profileValidator.ValidateOrThrowAsync(request, ct);
        var user = await GetUserAsync(userId, ct);

        user.FullName = request.FullName.Trim();
        user.PreferredVoice = request.PreferredVoice;
        user.PlaybackSpeed = request.PlaybackSpeed;

        await _uow.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        await _passwordValidator.ValidateOrThrowAsync(request, ct);
        var user = await GetUserAsync(userId, ct);

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new BusinessValidationException("CurrentPassword", "Mevcut şifre hatalı.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        RevokeAllSessions(userId);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await GetUserAsync(userId, ct);
        _users.Remove(user); // AppDbContext bunu yumuşak silmeye çevirir
        RevokeAllSessions(userId);
        await _uow.SaveChangesAsync(ct);
    }

    private void RevokeAllSessions(Guid userId)
    {
        var now = DateTime.UtcNow;
        foreach (var token in _refreshTokens.Query().Where(t => t.UserId == userId && t.RevokedAt == null))
            token.RevokedAt = now;
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken ct) =>
        await _users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Kullanıcı bulunamadı.");
}
