namespace SesliAsistan.Business.DTOs.Documents;

public record UploadDocumentCommand(
    Guid OwnerId,
    string? Title,
    string FileName,
    long Length,
    Stream Content);

public record DocumentListItemDto(
    Guid Id,
    string Title,
    string FileType,
    long FileSizeBytes,
    string Status,
    DateTime CreatedAt);

public record SectionNodeDto(
    Guid Id,
    string Title,
    int Level,
    int OrderIndex,
    List<SectionNodeDto> Children);

public record DocumentDetailDto(
    Guid Id,
    string Title,
    string OriginalFileName,
    string FileType,
    long FileSizeBytes,
    string Status,
    string? ErrorMessage,
    int? PageCount,
    int? CharacterCount,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    IReadOnlyList<SectionNodeDto> Sections);

public record SectionContentDto(Guid Id, string Title, int Level, string Content);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
