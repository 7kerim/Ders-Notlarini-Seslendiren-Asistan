using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.Business.Options;
using SesliAsistan.Business.Services;
using SesliAsistan.Business.Speech;
using SesliAsistan.Business.Validation;
using SesliAsistan.DataAccess.Context;
using SesliAsistan.DataAccess.Repositories;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SesliAsistan.Tests;

public class TestEmailSender : IEmailSender
{
    public string? LastRecipient { get; private set; }
    public string? LastCode { get; private set; }

    public Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken ct = default)
    {
        LastRecipient = toEmail;
        LastCode = code;
        return Task.CompletedTask;
    }
}

internal static class TestHelpers
{
    public static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static readonly JwtOptions Jwt = new()
    {
        Secret = "TEST-ICIN-EN-AZ-32-KARAKTERLIK-GIZLI-ANAHTAR!!",
        AccessTokenMinutes = 30,
        RefreshTokenDays = 14
    };

    public static readonly TtsOptions Tts = new()
    {
        Provider = "Mock",
        DefaultVoice = "tr-TR-EmelNeural",
        DefaultSpeed = 1.0m
    };

    public static AuthService CreateAuthService(AppDbContext db, TestEmailSender? emailSender = null) =>
        new(new UserRepository(db),
            new RefreshTokenRepository(db),
            new PasswordResetCodeRepository(db),
            emailSender ?? new TestEmailSender(),
            new UnitOfWork(db),
            new BCryptPasswordHasher(),
            new JwtTokenService(MsOptions.Create(Jwt)),
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            new ForgotPasswordRequestValidator(),
            new ResetPasswordRequestValidator(),
            MsOptions.Create(Jwt),
            NullLogger<AuthService>.Instance);

    public static TtsService CreateTtsService(AppDbContext db, ITtsProvider? ttsProvider = null, IFileStorage? storage = null) =>
        new(new AudioAssetRepository(db),
            new DocumentRepository(db),
            new GenericRepository<Entities.Entities.DocumentSection>(db),
            new SummaryRepository(db),
            ttsProvider ?? new MockTtsProvider(),
            storage ?? new TestStorage(),
            new UnitOfWork(db),
            MsOptions.Create(Tts),
            NullLogger<TtsService>.Instance);
}

public class TestStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = new();

    public Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        var key = $"{folder}/{Guid.NewGuid():N}{extension}";
        _files[key] = ms.ToArray();
        return Task.FromResult(key);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        if (_files.TryGetValue(storageKey, out var bytes))
            return Task.FromResult<Stream>(new MemoryStream(bytes));
        throw new FileNotFoundException();
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        _files.Remove(storageKey);
        return Task.CompletedTask;
    }
}
