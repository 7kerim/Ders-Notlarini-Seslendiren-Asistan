using SesliAsistan.Entities.Common;

namespace SesliAsistan.Entities.Entities;

/// <summary>FG-1: Şifre sıfırlama için tek kullanımlık doğrulama kodu. SHA-256 özeti olarak saklanır.</summary>
public class PasswordResetCode : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string CodeHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public int FailedAttempts { get; set; }
    public bool IsUsed { get; set; }

    public bool IsValid => !IsUsed && FailedAttempts < 5 && DateTime.UtcNow < ExpiresAt;
}
