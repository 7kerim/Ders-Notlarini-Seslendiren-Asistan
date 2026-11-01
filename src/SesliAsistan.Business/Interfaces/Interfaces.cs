using SesliAsistan.Business.DTOs.Auth;
using SesliAsistan.Business.DTOs.Documents;
using SesliAsistan.Business.DTOs.Learning;
using SesliAsistan.Business.TextExtraction;
using SesliAsistan.Entities.Entities;
using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Business.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}

public interface IEmailSender
{
    Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken ct = default);
}

public interface IUserService
{
    Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct = default);
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
    /// <summary>KVKK: kullanıcının hesabını silme hakkı.</summary>
    Task DeleteAccountAsync(Guid userId, CancellationToken ct = default);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user);
    (string PlainToken, string TokenHash) CreateRefreshToken();
    string HashToken(string token);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IDocumentService
{
    Task<DocumentDetailDto> UploadAsync(UploadDocumentCommand command, CancellationToken ct = default);
    Task<PagedResult<DocumentListItemDto>> GetListAsync(Guid ownerId, int page, int pageSize, string? search, CancellationToken ct = default);
    Task<DocumentDetailDto> GetDetailAsync(Guid ownerId, Guid documentId, CancellationToken ct = default);
    Task<SectionContentDto> GetSectionAsync(Guid ownerId, Guid documentId, Guid sectionId, CancellationToken ct = default);
    Task DeleteAsync(Guid ownerId, Guid documentId, CancellationToken ct = default);
    Task ReprocessAsync(Guid ownerId, Guid documentId, CancellationToken ct = default);
}

/// <summary>Belgeyi arka planda işleyen (metin çıkarma + konu ağacı) bileşen.</summary>
public interface IDocumentProcessor
{
    Task ProcessAsync(Guid documentId, CancellationToken ct = default);
}

/// <summary>Yükleme isteğini bekletmeden işleme kuyruğu (NFR: asenkron işleme).</summary>
public interface IDocumentProcessingQueue
{
    ValueTask EnqueueAsync(Guid documentId, CancellationToken ct = default);
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct);
}

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

public interface IFileValidator
{
    /// <summary>Uzantı, boyut ve dosya imzasını (magic bytes) doğrular; geçerliyse dosya tipini döner.</summary>
    DocumentFileType Validate(string fileName, long length, Stream content);
}

public interface ITextExtractor
{
    DocumentFileType SupportedType { get; }
    Task<ExtractionResult> ExtractAsync(Stream content, CancellationToken ct = default);
}

public interface ITtsProvider
{
    Task<byte[]> SynthesizeAsync(string ssml, string voice, decimal speed, CancellationToken ct = default);
}

public interface ITtsService
{
    Task<AudioResultDto> SynthesizeSectionAudioAsync(Guid userId, Guid documentId, Guid sectionId, string? voice, decimal? speed, CancellationToken ct = default);
    Task<AudioResultDto> SynthesizeSummaryAudioAsync(Guid userId, Guid summaryId, string? voice, decimal? speed, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, long FileLength)> GetAudioStreamAsync(Guid audioId, CancellationToken ct = default);
    IReadOnlyList<VoiceDto> GetAvailableVoices();
}

public interface IListeningProgressService
{
    Task SaveProgressAsync(Guid userId, Guid documentId, SaveProgressRequest request, CancellationToken ct = default);
    Task<ListeningProgressDto?> GetProgressAsync(Guid userId, Guid documentId, CancellationToken ct = default);
}

public interface ITextRankSummarizer
{
    string Summarize(string text, SummaryLength length);
}

public interface ISummaryService
{
    Task<SummaryDto> CreateSummaryAsync(Guid userId, Guid documentId, Guid sectionId, SummaryLength length, CancellationToken ct = default);
    Task<SummaryDto> GetSummaryAsync(Guid userId, Guid summaryId, CancellationToken ct = default);
}
