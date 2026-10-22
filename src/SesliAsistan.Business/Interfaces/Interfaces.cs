using SesliAsistan.Business.DTOs.Auth;
using SesliAsistan.Business.DTOs.Documents;
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
