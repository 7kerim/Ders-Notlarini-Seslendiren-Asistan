using SesliAsistan.Entities.Common;
using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Entities.Entities;

/// <summary>FG-2: Kullanıcının yüklediği ders notu (PDF / .docx).</summary>
public class Document : BaseEntity, ISoftDeletable
{
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public string Title { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string StoragePath { get; set; } = null!;
    public DocumentFileType FileType { get; set; }
    public long FileSizeBytes { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;
    public string? ErrorMessage { get; set; }
    public int? PageCount { get; set; }
    public int? CharacterCount { get; set; }
    public string? ExtractedText { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<DocumentSection> Sections { get; set; } = new List<DocumentSection>();
}
