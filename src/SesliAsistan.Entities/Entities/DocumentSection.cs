using SesliAsistan.Entities.Common;

namespace SesliAsistan.Entities.Entities;

/// <summary>FG-5: Konu ağacı düğümü. ParentId ile kendine referans veren hiyerarşik yapı.</summary>
public class DocumentSection : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public Guid? ParentId { get; set; }
    public DocumentSection? Parent { get; set; }
    public ICollection<DocumentSection> Children { get; set; } = new List<DocumentSection>();

    public string Title { get; set; } = null!;
    public int Level { get; set; }
    public int OrderIndex { get; set; }
    public string Content { get; set; } = string.Empty;
}
