namespace SesliAsistan.Business.DTOs.Auth;

public record RegisterRequest(string Email, string Password, string FullName);

public record LoginRequest(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    string PreferredVoice,
    decimal PlaybackSpeed,
    DateTime CreatedAt);

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    UserDto User);

public record UpdateProfileRequest(string FullName, string PreferredVoice, decimal PlaybackSpeed);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
