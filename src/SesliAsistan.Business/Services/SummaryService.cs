using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SesliAsistan.Business.DTOs.Learning;
using SesliAsistan.Business.Exceptions;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.DataAccess.Repositories;
using SesliAsistan.Entities.Entities;
using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Business.Services;

public class SummaryService : ISummaryService
{
    private readonly ISummaryRepository _summaries;
    private readonly IDocumentRepository _documents;
    private readonly IGenericRepository<DocumentSection> _sections;
    private readonly ITextRankSummarizer _summarizer;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SummaryService> _logger;

    public SummaryService(
        ISummaryRepository summaries,
        IDocumentRepository documents,
        IGenericRepository<DocumentSection> sections,
        ITextRankSummarizer summarizer,
        IUnitOfWork uow,
        ILogger<SummaryService> logger)
    {
        _summaries = summaries;
        _documents = documents;
        _sections = sections;
        _summarizer = summarizer;
        _uow = uow;
        _logger = logger;
    }

    public async Task<SummaryDto> CreateSummaryAsync(
        Guid userId, Guid documentId, Guid sectionId, SummaryLength length, CancellationToken ct = default)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || document.OwnerId != userId)
            throw new NotFoundException("Belge bulunamadı.");

        var section = await _sections.Query()
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.DocumentId == documentId, ct);

        if (section is null)
            throw new NotFoundException("Bölüm bulunamadı.");

        var summaryText = _summarizer.Summarize(section.Content, length);

        var summary = new Summary
        {
            DocumentId = documentId,
            OwnerId = userId,
            Length = length,
            Content = summaryText,
            Privacy = PrivacyLevel.Private
        };

        await _summaries.AddAsync(summary, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Özet oluşturuldu: Belge {DocId}, Bölüm {SecId}, Uzunluk: {Len}",
            documentId, sectionId, length);

        return new SummaryDto(
            summary.Id,
            summary.DocumentId,
            sectionId,
            summary.Length,
            summary.Content,
            summary.CreatedAt);
    }

    public async Task<SummaryDto> GetSummaryAsync(Guid userId, Guid summaryId, CancellationToken ct = default)
    {
        var summary = await _summaries.GetByIdAsync(summaryId, ct);
        if (summary is null || summary.OwnerId != userId)
            throw new NotFoundException("Özet bulunamadı.");

        return new SummaryDto(
            summary.Id,
            summary.DocumentId,
            null,
            summary.Length,
            summary.Content,
            summary.CreatedAt);
    }
}
