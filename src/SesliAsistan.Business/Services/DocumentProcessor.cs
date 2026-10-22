using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.Business.TextExtraction;
using SesliAsistan.DataAccess.Repositories;
using SesliAsistan.Entities.Entities;
using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Business.Services;

/// <summary>
/// Belge işleme hattı: Uploaded → Processing → (Ready | Failed).
/// Metni çıkarır, konu ağacını (DocumentSection) oluşturur ve veritabanına yazar.
/// </summary>
public class DocumentProcessor : IDocumentProcessor
{
    private readonly IDocumentRepository _documents;
    private readonly IGenericRepository<DocumentSection> _sections;
    private readonly IUnitOfWork _uow;
    private readonly IFileStorage _storage;
    private readonly IEnumerable<ITextExtractor> _extractors;
    private readonly IDistributedCache _cache;
    private readonly ILogger<DocumentProcessor> _logger;

    public DocumentProcessor(
        IDocumentRepository documents,
        IGenericRepository<DocumentSection> sections,
        IUnitOfWork uow,
        IFileStorage storage,
        IEnumerable<ITextExtractor> extractors,
        IDistributedCache cache,
        ILogger<DocumentProcessor> logger)
    {
        _documents = documents;
        _sections = sections;
        _uow = uow;
        _storage = storage;
        _extractors = extractors;
        _cache = cache;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || document.Status is DocumentStatus.Ready or DocumentStatus.Processing) return;

        document.Status = DocumentStatus.Processing;
        await _uow.SaveChangesAsync(ct);

        try
        {
            var extractor = _extractors.FirstOrDefault(e => e.SupportedType == document.FileType)
                ?? throw new NotSupportedException($"{document.FileType} için metin çıkarıcı bulunamadı.");

            ExtractionResult result;
            await using (var stream = await _storage.OpenReadAsync(document.StoragePath, ct))
                result = await extractor.ExtractAsync(stream, ct);

            // Yeniden işleme durumunda eski bölümleri temizle
            var oldSections = await _sections.Query().Where(s => s.DocumentId == document.Id).ToListAsync(ct);
            foreach (var old in oldSections) _sections.Remove(old);

            var linked = SectionBuilder.LinkParents(result.Sections);
            var entities = new List<DocumentSection>(linked.Count);
            for (var i = 0; i < linked.Count; i++)
            {
                var (section, parentIndex) = linked[i];
                var entity = new DocumentSection
                {
                    DocumentId = document.Id,
                    ParentId = parentIndex is int p ? entities[p].Id : null,
                    Title = Truncate(section.Title, 300),
                    Level = section.Level,
                    OrderIndex = i,
                    Content = section.Content
                };
                entities.Add(entity);
                await _sections.AddAsync(entity, ct);
            }

            document.ExtractedText = result.FullText;
            document.CharacterCount = result.FullText.Length;
            document.PageCount = result.PageCount;
            document.Status = DocumentStatus.Ready;
            document.ProcessedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Belge işlendi: {DocumentId}, {SectionCount} bölüm, {Chars} karakter",
                document.Id, entities.Count, document.CharacterCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Belge işlenemedi: {DocumentId}", document.Id);
            document.Status = DocumentStatus.Failed;
            document.ErrorMessage = Truncate(ex.Message, 1000);
            await _uow.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            await _cache.RemoveAsync(DocumentService.CacheKey(document.Id), CancellationToken.None);
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
