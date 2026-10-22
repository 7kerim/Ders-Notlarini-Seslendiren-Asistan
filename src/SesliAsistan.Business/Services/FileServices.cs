using System.Threading.Channels;
using Microsoft.Extensions.Options;
using SesliAsistan.Business.Exceptions;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.Business.Options;
using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Business.Services;

public class FileValidator : IFileValidator
{
    private static readonly byte[] PdfSignature = "%PDF"u8.ToArray();
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04]; // .docx = ZIP paketi

    private readonly FileStorageOptions _options;

    public FileValidator(IOptions<FileStorageOptions> options) => _options = options.Value;

    public DocumentFileType Validate(string fileName, long length, Stream content)
    {
        var type = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => DocumentFileType.Pdf,
            ".docx" => DocumentFileType.Docx,
            _ => throw new BusinessValidationException("File", "Yalnızca PDF ve .docx dosyaları yüklenebilir.")
        };

        if (length <= 0)
            throw new BusinessValidationException("File", "Dosya boş.");

        if (length > _options.MaxFileSizeBytes)
            throw new BusinessValidationException("File",
                $"Dosya boyutu en fazla {_options.MaxFileSizeBytes / (1024 * 1024)} MB olabilir.");

        // Uzantı değiştirilerek yüklenen sahte dosyaları engellemek için dosya imzası kontrolü
        var header = new byte[4];
        var read = content.Read(header, 0, header.Length);
        if (content.CanSeek) content.Position = 0;

        var expected = type == DocumentFileType.Pdf ? PdfSignature : ZipSignature;
        if (read < 4 || !header.AsSpan().SequenceEqual(expected))
            throw new BusinessValidationException("File", "Dosya içeriği uzantısıyla uyuşmuyor veya dosya bozuk.");

        return type;
    }
}

/// <summary>Geliştirme ortamı için yerel disk depolaması. Üretimde S3/Azure Blob ile değiştirilebilir.</summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default)
    {
        var key = Path.Combine(folder, $"{Guid.NewGuid():N}{extension}").Replace('\\', '/');
        var fullPath = ResolveSafe(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolveSafe(storageKey);
        if (!File.Exists(path)) throw new NotFoundException("Dosya depolamada bulunamadı.");
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolveSafe(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>Path traversal ("../") saldırılarına karşı yolun kök klasör içinde kaldığını doğrular.</summary>
    private string ResolveSafe(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root, StringComparison.Ordinal))
            throw new ForbiddenAppException("Geçersiz dosya yolu.");
        return full;
    }
}

public class DocumentProcessingQueue : IDocumentProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(500)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = false
    });

    public ValueTask EnqueueAsync(Guid documentId, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(documentId, ct);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
