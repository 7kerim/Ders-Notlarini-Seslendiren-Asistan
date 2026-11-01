using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SesliAsistan.Business.DTOs.Learning;
using SesliAsistan.Business.Exceptions;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.Business.Options;
using SesliAsistan.Business.Speech;
using SesliAsistan.DataAccess.Repositories;
using SesliAsistan.Entities.Entities;

namespace SesliAsistan.Business.Services;

public class TtsService : ITtsService
{
    private static readonly IReadOnlyList<VoiceDto> SupportedVoices =
    [
        new("tr-TR-EmelNeural", "Emel (Doğal Türkçe Kadın)", "Female", "tr-TR"),
        new("tr-TR-AhmetNeural", "Ahmet (Doğal Türkçe Erkek)", "Male", "tr-TR")
    ];

    private readonly IAudioAssetRepository _audioAssets;
    private readonly IDocumentRepository _documents;
    private readonly IGenericRepository<DocumentSection> _sections;
    private readonly ISummaryRepository _summaries;
    private readonly ITtsProvider _ttsProvider;
    private readonly IFileStorage _storage;
    private readonly IUnitOfWork _uow;
    private readonly TtsOptions _options;
    private readonly ILogger<TtsService> _logger;

    public TtsService(
        IAudioAssetRepository audioAssets,
        IDocumentRepository documents,
        IGenericRepository<DocumentSection> sections,
        ISummaryRepository summaries,
        ITtsProvider ttsProvider,
        IFileStorage storage,
        IUnitOfWork uow,
        IOptions<TtsOptions> options,
        ILogger<TtsService> logger)
    {
        _audioAssets = audioAssets;
        _documents = documents;
        _sections = sections;
        _summaries = summaries;
        _ttsProvider = ttsProvider;
        _storage = storage;
        _uow = uow;
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<VoiceDto> GetAvailableVoices() => SupportedVoices;

    public async Task<AudioResultDto> SynthesizeSectionAudioAsync(
        Guid userId, Guid documentId, Guid sectionId, string? voice, decimal? speed, CancellationToken ct = default)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || document.OwnerId != userId)
            throw new NotFoundException("Belge bulunamadı.");

        var section = await _sections.Query()
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.DocumentId == documentId, ct);

        if (section is null)
            throw new NotFoundException("Bölüm bulunamadı.");

        var safeVoice = string.IsNullOrWhiteSpace(voice) ? _options.DefaultVoice : voice.Trim();
        var safeSpeed = Math.Clamp(speed ?? _options.DefaultSpeed, 0.5m, 2.0m);

        return await GetOrCreateAudioAsync(
            documentId: documentId,
            sectionId: sectionId,
            summaryId: null,
            text: section.Content,
            title: section.Title,
            voice: safeVoice,
            speed: safeSpeed,
            ct: ct);
    }

    public async Task<AudioResultDto> SynthesizeSummaryAudioAsync(
        Guid userId, Guid summaryId, string? voice, decimal? speed, CancellationToken ct = default)
    {
        var summary = await _summaries.GetByIdAsync(summaryId, ct);
        if (summary is null || summary.OwnerId != userId)
            throw new NotFoundException("Özet bulunamadı.");

        var safeVoice = string.IsNullOrWhiteSpace(voice) ? _options.DefaultVoice : voice.Trim();
        var safeSpeed = Math.Clamp(speed ?? _options.DefaultSpeed, 0.5m, 2.0m);

        return await GetOrCreateAudioAsync(
            documentId: summary.DocumentId,
            sectionId: null,
            summaryId: summaryId,
            text: summary.Content,
            title: "Özet",
            voice: safeVoice,
            speed: safeSpeed,
            ct: ct);
    }

    public async Task<(Stream Stream, string ContentType, long FileLength)> GetAudioStreamAsync(Guid audioId, CancellationToken ct = default)
    {
        var asset = await _audioAssets.GetByIdAsync(audioId, ct);
        if (asset is null)
            throw new NotFoundException("Ses kaydı bulunamadı.");

        var stream = await _storage.OpenReadAsync(asset.StorageKey, ct);
        return (stream, "audio/mpeg", stream.Length);
    }

    private async Task<AudioResultDto> GetOrCreateAudioAsync(
        Guid documentId,
        Guid? sectionId,
        Guid? summaryId,
        string text,
        string title,
        string voice,
        decimal speed,
        CancellationToken ct)
    {
        var normalizedText = text?.Trim() ?? string.Empty;
        var contentHash = ComputeSha256(normalizedText);

        // 1. Önbellek kontrolü (aynı metin, aynı ses, aynı hız)
        var cached = await _audioAssets.FindByHashAsync(contentHash, voice, speed, ct);
        if (cached is not null)
        {
            _logger.LogInformation("TTS önbellek isabeti (Cache Hit): {ContentHash}, Ses: {Voice}, Hız: {Speed}",
                contentHash, voice, speed);

            return new AudioResultDto(
                cached.Id,
                $"/api/v1/audio/{cached.Id}",
                cached.Voice,
                cached.Speed,
                cached.DurationSeconds,
                FromCache: true);
        }

        // 2. Önbellekte yoksa: metni parçala ve seslendir
        _logger.LogInformation("TTS önbellek ıskası (Cache Miss). Ses sentezleniyor: {ContentHash}", contentHash);
        var chunks = TextChunker.SplitIntoChunks(normalizedText);
        if (chunks.Count == 0)
        {
            chunks.Add("İçerik boş.");
        }

        using var memoryStream = new MemoryStream();
        foreach (var chunk in chunks)
        {
            var ssml = SsmlBuilder.BuildSsml(chunk, voice, speed, addBreakAfterTitle: false);
            var audioBytes = await _ttsProvider.SynthesizeAsync(ssml, voice, speed, ct);
            await memoryStream.WriteAsync(audioBytes, ct);
        }

        memoryStream.Position = 0;
        var storageKey = await _storage.SaveAsync(memoryStream, $"audio/{documentId:N}", ".mp3", ct);

        // Yaklaşık süre hesabı (Türkçe ortalama ~15 karakter/sn at 1.0x hız)
        int estimatedDuration = Math.Max(1, (int)Math.Round((normalizedText.Length / 15.0) / (double)speed));

        var asset = new AudioAsset
        {
            DocumentId = documentId,
            SectionId = sectionId,
            SummaryId = summaryId,
            Voice = voice,
            Speed = speed,
            ContentHash = contentHash,
            StorageKey = storageKey,
            DurationSeconds = estimatedDuration
        };

        await _audioAssets.AddAsync(asset, ct);
        await _uow.SaveChangesAsync(ct);

        return new AudioResultDto(
            asset.Id,
            $"/api/v1/audio/{asset.Id}",
            asset.Voice,
            asset.Speed,
            asset.DurationSeconds,
            FromCache: false);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
