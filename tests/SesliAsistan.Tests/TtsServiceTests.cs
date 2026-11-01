using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SesliAsistan.Business.Exceptions;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.Entities.Entities;
using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Tests;

public class TtsServiceTests
{
    private class TrackingTtsProvider : ITtsProvider
    {
        public int CallCount { get; private set; }

        public Task<byte[]> SynthesizeAsync(string ssml, string voice, decimal speed, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new byte[] { 0xFF, 0xFB, 0x90, 0x64 });
        }
    }

    private static async Task<(User User, Document Doc, DocumentSection Section)> SeedDocumentAsync(DataAccess.Context.AppDbContext db)
    {
        var user = new User
        {
            Email = "ogrenci@firat.edu.tr",
            FullName = "Test Öğrenci",
            PasswordHash = "hash"
        };
        await db.Users.AddAsync(user);

        var doc = new Document
        {
            OwnerId = user.Id,
            Title = "Yapay Zeka Giriş",
            OriginalFileName = "ai.pdf",
            StoragePath = "documents/test.pdf",
            FileType = DocumentFileType.Pdf,
            Status = DocumentStatus.Ready
        };
        await db.Documents.AddAsync(doc);

        var section = new DocumentSection
        {
            DocumentId = doc.Id,
            Title = "Giriş ve Temel Kavramlar",
            Content = "Yapay zeka makinelerin insan benzeri kararlar vermesini sağlayan teknolojidir.",
            Level = 1,
            OrderIndex = 1
        };
        await db.DocumentSections.AddAsync(section);
        await db.SaveChangesAsync();

        return (user, doc, section);
    }

    [Fact]
    public async Task SynthesizeSectionAudio_CacheMiss_CallsProviderAndCreatesAudioAsset()
    {
        await using var db = TestHelpers.CreateDb();
        var (user, doc, section) = await SeedDocumentAsync(db);

        var provider = new TrackingTtsProvider();
        var sut = TestHelpers.CreateTtsService(db, provider);

        var result = await sut.SynthesizeSectionAudioAsync(user.Id, doc.Id, section.Id, "tr-TR-EmelNeural", 1.0m);

        result.FromCache.Should().BeFalse();
        result.AudioId.Should().NotBeEmpty();
        provider.CallCount.Should().Be(1);

        var assetInDb = await db.AudioAssets.SingleOrDefaultAsync(a => a.Id == result.AudioId);
        assetInDb.Should().NotBeNull();
        assetInDb!.Voice.Should().Be("tr-TR-EmelNeural");
        assetInDb.Speed.Should().Be(1.0m);
    }

    [Fact]
    public async Task SynthesizeSectionAudio_CacheHit_DoesNotCallProviderAndReturnsCachedAsset()
    {
        await using var db = TestHelpers.CreateDb();
        var (user, doc, section) = await SeedDocumentAsync(db);

        var provider = new TrackingTtsProvider();
        var sut = TestHelpers.CreateTtsService(db, provider);

        // İlk çağrı: önbellek ıskası, sağlayıcı çağrılır
        var first = await sut.SynthesizeSectionAudioAsync(user.Id, doc.Id, section.Id, "tr-TR-EmelNeural", 1.0m);
        first.FromCache.Should().BeFalse();
        provider.CallCount.Should().Be(1);

        // İkinci çağrı: aynı metin, ses ve hız -> önbellek isabeti, sağlayıcı tekrar çağrılmaz
        var second = await sut.SynthesizeSectionAudioAsync(user.Id, doc.Id, section.Id, "tr-TR-EmelNeural", 1.0m);
        second.FromCache.Should().BeTrue();
        second.AudioId.Should().Be(first.AudioId);
        provider.CallCount.Should().Be(1); // Çağrı sayısı artmamalı
    }

    [Fact]
    public async Task SynthesizeSectionAudio_SameTextDifferentSpeed_CreatesDistinctAssets()
    {
        await using var db = TestHelpers.CreateDb();
        var (user, doc, section) = await SeedDocumentAsync(db);

        var provider = new TrackingTtsProvider();
        var sut = TestHelpers.CreateTtsService(db, provider);

        var audioNormal = await sut.SynthesizeSectionAudioAsync(user.Id, doc.Id, section.Id, "tr-TR-EmelNeural", 1.0m);
        var audioFast = await sut.SynthesizeSectionAudioAsync(user.Id, doc.Id, section.Id, "tr-TR-EmelNeural", 1.5m);

        audioNormal.AudioId.Should().NotBe(audioFast.AudioId);
        provider.CallCount.Should().Be(2);

        (await db.AudioAssets.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task GetAudioStream_ValidId_ReturnsAudioStream()
    {
        await using var db = TestHelpers.CreateDb();
        var (user, doc, section) = await SeedDocumentAsync(db);

        var sut = TestHelpers.CreateTtsService(db);
        var result = await sut.SynthesizeSectionAudioAsync(user.Id, doc.Id, section.Id, "tr-TR-EmelNeural", 1.0m);

        var (stream, contentType, length) = await sut.GetAudioStreamAsync(result.AudioId);

        stream.Should().NotBeNull();
        contentType.Should().Be("audio/mpeg");
        length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SynthesizeSectionAudio_NonExistentSection_ThrowsNotFoundException()
    {
        await using var db = TestHelpers.CreateDb();
        var (user, doc, _) = await SeedDocumentAsync(db);

        var sut = TestHelpers.CreateTtsService(db);
        var act = () => sut.SynthesizeSectionAudioAsync(user.Id, doc.Id, Guid.NewGuid(), null, null);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
