using Microsoft.Extensions.Logging;
using SesliAsistan.Business.DTOs.Learning;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.DataAccess.Repositories;
using SesliAsistan.Entities.Entities;

namespace SesliAsistan.Business.Services;

public class ListeningProgressService : IListeningProgressService
{
    private readonly IListeningProgressRepository _progressRepository;
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ListeningProgressService> _logger;

    public ListeningProgressService(
        IListeningProgressRepository progressRepository,
        IDocumentRepository documents,
        IUnitOfWork uow,
        ILogger<ListeningProgressService> logger)
    {
        _progressRepository = progressRepository;
        _documents = documents;
        _uow = uow;
        _logger = logger;
    }

    public async Task SaveProgressAsync(Guid userId, Guid documentId, SaveProgressRequest request, CancellationToken ct = default)
    {
        var existing = await _progressRepository.GetByDocumentAsync(userId, documentId, ct);

        if (existing is not null)
        {
            existing.SectionId = request.SectionId;
            existing.PositionSeconds = Math.Max(0, request.PositionSeconds);
            existing.UpdatedAt = DateTime.UtcNow;
            _progressRepository.Update(existing);
        }
        else
        {
            var newProgress = new ListeningProgress
            {
                UserId = userId,
                DocumentId = documentId,
                SectionId = request.SectionId,
                PositionSeconds = Math.Max(0, request.PositionSeconds)
            };
            await _progressRepository.AddAsync(newProgress, ct);
        }

        await _uow.SaveChangesAsync(ct);
        _logger.LogDebug("Dinleme konumu kaydedildi: Kullanıcı: {UserId}, Belge: {DocumentId}, Saniye: {Pos}",
            userId, documentId, request.PositionSeconds);
    }

    public async Task<ListeningProgressDto?> GetProgressAsync(Guid userId, Guid documentId, CancellationToken ct = default)
    {
        var progress = await _progressRepository.GetByDocumentAsync(userId, documentId, ct);
        if (progress is null)
            return null;

        return new ListeningProgressDto(
            progress.DocumentId,
            progress.SectionId,
            progress.PositionSeconds,
            progress.UpdatedAt ?? progress.CreatedAt);
    }
}
