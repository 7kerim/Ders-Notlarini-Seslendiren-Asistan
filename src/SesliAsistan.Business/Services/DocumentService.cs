using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using SesliAsistan.Business.DTOs.Documents;
using SesliAsistan.Business.Exceptions;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.DataAccess.Repositories;
using SesliAsistan.Entities.Entities;
using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Business.Services;

public class DocumentService : IDocumentService
{
    private const int MaxPageSize = 50;
    private static readonly DistributedCacheEntryOptions CacheOptions =
        new() { SlidingExpiration = TimeSpan.FromMinutes(10) };

    private readonly IDocumentRepository _documents;
    private readonly IGenericRepository<DocumentSection> _sections;
    private readonly IUnitOfWork _uow;
    private readonly IFileValidator _fileValidator;
    private readonly IFileStorage _storage;
    private readonly IDocumentProcessingQueue _queue;
    private readonly IDistributedCache _cache;

    public DocumentService(
        IDocumentRepository documents,
        IGenericRepository<DocumentSection> sections,
        IUnitOfWork uow,
        IFileValidator fileValidator,
        IFileStorage storage,
        IDocumentProcessingQueue queue,
        IDistributedCache cache)
    {
        _documents = documents;
        _sections = sections;
        _uow = uow;
        _fileValidator = fileValidator;
        _storage = storage;
        _queue = queue;
        _cache = cache;
    }

    public async Task<DocumentDetailDto> UploadAsync(UploadDocumentCommand cmd, CancellationToken ct = default)
    {
        var fileType = _fileValidator.Validate(cmd.FileName, cmd.Length, cmd.Content);
        var extension = fileType == DocumentFileType.Pdf ? ".pdf" : ".docx";
        var storageKey = await _storage.SaveAsync(cmd.Content, $"documents/{cmd.OwnerId:N}", extension, ct);

        var title = string.IsNullOrWhiteSpace(cmd.Title)
            ? Path.GetFileNameWithoutExtension(cmd.FileName)
            : cmd.Title.Trim();

        var document = new Document
        {
            OwnerId = cmd.OwnerId,
            Title = title.Length > 200 ? title[..200] : title,
            OriginalFileName = Path.GetFileName(cmd.FileName),
            StoragePath = storageKey,
            FileType = fileType,
            FileSizeBytes = cmd.Length,
            Status = DocumentStatus.Uploaded
        };

        await _documents.AddAsync(document, ct);
        await _uow.SaveChangesAsync(ct);

        // Metin çıkarma arka planda yapılır; istemci durumu GET /documents/{id} ile izler
        await _queue.EnqueueAsync(document.Id, ct);

        return ToDetailDto(document, []);
    }

    public async Task<PagedResult<DocumentListItemDto>> GetListAsync(
        Guid ownerId, int page, int pageSize, string? search, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (items, total) = await _documents.GetPagedByOwnerAsync(ownerId, page, pageSize, search, ct);
        var dtos = items.Select(d => new DocumentListItemDto(
            d.Id, d.Title, d.FileType.ToString(), d.FileSizeBytes, d.Status.ToString(), d.CreatedAt)).ToList();

        return new PagedResult<DocumentListItemDto>(dtos, page, pageSize, total);
    }

    public async Task<DocumentDetailDto> GetDetailAsync(Guid ownerId, Guid documentId, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(documentId);
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
        {
            var dto = JsonSerializer.Deserialize<CachedDetail>(cached);
            if (dto is not null && dto.OwnerId == ownerId) return dto.Detail;
        }

        var document = await _documents.GetWithSectionsAsync(documentId, ct);
        EnsureOwned(document, ownerId);

        var detail = ToDetailDto(document!, BuildTree(document!.Sections));

        // Yalnızca işlenmiş belgeler önbelleğe alınır (durum değişmeyeceği için)
        if (document.Status == DocumentStatus.Ready)
            await _cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(new CachedDetail(ownerId, detail)), CacheOptions, ct);

        return detail;
    }

    public async Task<SectionContentDto> GetSectionAsync(Guid ownerId, Guid documentId, Guid sectionId, CancellationToken ct = default)
    {
        var section = await _sections.Query()
            .AsNoTracking()
            .Where(s => s.Id == sectionId && s.DocumentId == documentId && s.Document.OwnerId == ownerId)
            .Select(s => new SectionContentDto(s.Id, s.Title, s.Level, s.Content))
            .FirstOrDefaultAsync(ct);

        return section ?? throw new NotFoundException("Bölüm bulunamadı.");
    }

    public async Task DeleteAsync(Guid ownerId, Guid documentId, CancellationToken ct = default)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        EnsureOwned(document, ownerId);

        _documents.Remove(document!);
        await _uow.SaveChangesAsync(ct);
        await _storage.DeleteAsync(document!.StoragePath, ct);
        await _cache.RemoveAsync(CacheKey(documentId), ct);
    }

    public async Task ReprocessAsync(Guid ownerId, Guid documentId, CancellationToken ct = default)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        EnsureOwned(document, ownerId);

        if (document!.Status != DocumentStatus.Failed)
            throw new ConflictException("Yalnızca işlenemeyen belgeler yeniden işlenebilir.");

        document.Status = DocumentStatus.Uploaded;
        document.ErrorMessage = null;
        await _uow.SaveChangesAsync(ct);
        await _queue.EnqueueAsync(document.Id, ct);
    }

    internal static string CacheKey(Guid documentId) => $"document:{documentId}:detail";

    /// <summary>Başkasına ait belgeler için de 404 dönülür; belgenin varlığı sızdırılmaz.</summary>
    private static void EnsureOwned(Document? document, Guid ownerId)
    {
        if (document is null || document.OwnerId != ownerId)
            throw new NotFoundException("Belge bulunamadı.");
    }

    internal static List<SectionNodeDto> BuildTree(IEnumerable<DocumentSection> sections)
    {
        var ordered = sections.OrderBy(s => s.OrderIndex).ToList();
        var nodes = ordered.ToDictionary(
            s => s.Id,
            s => new SectionNodeDto(s.Id, s.Title, s.Level, s.OrderIndex, new List<SectionNodeDto>()));

        var roots = new List<SectionNodeDto>();
        foreach (var s in ordered)
        {
            if (s.ParentId is Guid pid && nodes.TryGetValue(pid, out var parent))
                parent.Children.Add(nodes[s.Id]);
            else
                roots.Add(nodes[s.Id]);
        }
        return roots;
    }

    private static DocumentDetailDto ToDetailDto(Document d, IReadOnlyList<SectionNodeDto> tree) =>
        new(d.Id, d.Title, d.OriginalFileName, d.FileType.ToString(), d.FileSizeBytes, d.Status.ToString(),
            d.ErrorMessage, d.PageCount, d.CharacterCount, d.CreatedAt, d.ProcessedAt, tree);

    private record CachedDetail(Guid OwnerId, DocumentDetailDto Detail);
}
