namespace SesliAsistan.Entities.Common;

/// <summary>Tüm tabloların ortak alanları (birincil anahtar ve zaman damgaları).</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Kalıcı silme yerine "yumuşak silme" uygulanan varlıklar (KVKK: hesap/belge silme talebi).</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
