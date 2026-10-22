using SesliAsistan.Entities.Common;
using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Entities.Entities;

/// <summary>FG-1: Sistem kullanıcısı (Öğrenci / Yönetici).</summary>
public class User : BaseEntity, ISoftDeletable
{
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public UserRole Role { get; set; } = UserRole.Student;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    // FG-3: Kullanıcının varsayılan seslendirme tercihleri
    public string PreferredVoice { get; set; } = "tr-TR-Female-1";
    public decimal PlaybackSpeed { get; set; } = 1.0m;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
