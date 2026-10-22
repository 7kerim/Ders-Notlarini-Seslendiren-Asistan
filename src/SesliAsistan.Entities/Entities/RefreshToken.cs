using SesliAsistan.Entities.Common;

namespace SesliAsistan.Entities.Entities;

/// <summary>JWT yenileme anahtarı. Veritabanında düz metin değil SHA-256 özeti saklanır.</summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
