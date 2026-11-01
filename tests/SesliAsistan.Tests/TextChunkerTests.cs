using FluentAssertions;
using SesliAsistan.Business.Speech;

namespace SesliAsistan.Tests;

public class TextChunkerTests
{
    [Fact]
    public void SplitIntoChunks_EmptyOrWhitespace_ReturnsEmptyList()
    {
        TextChunker.SplitIntoChunks("").Should().BeEmpty();
        TextChunker.SplitIntoChunks("   ").Should().BeEmpty();
        TextChunker.SplitIntoChunks(null).Should().BeEmpty();
    }

    [Fact]
    public void SplitIntoChunks_SingleSentence_ReturnsSingleChunk()
    {
        var text = "Bu bir tasarım dersi projesidir.";
        var result = TextChunker.SplitIntoChunks(text);

        result.Should().HaveCount(1);
        result[0].Should().Be(text);
    }

    [Fact]
    public void SplitIntoChunks_MultipleSentences_SplitsAtSentenceBoundaries()
    {
        var text = "Birinci cümle burada bitiyor. İkinci cümle ise burada başlıyor! Peki üçüncü cümle soru mu?";
        var sentences = TextChunker.SplitIntoSentences(text);

        sentences.Should().HaveCount(3);
        sentences[0].Should().Be("Birinci cümle burada bitiyor.");
        sentences[1].Should().Be("İkinci cümle ise burada başlıyor!");
        sentences[2].Should().Be("Peki üçüncü cümle soru mu?");
    }

    [Fact]
    public void SplitIntoChunks_TurkishAbbreviations_DoesNotSplitAtAbbreviation()
    {
        var text = "Dr. Ahmet Bey ve Prof. Mehmet Bey toplantıya katıldı. Konu görüşüldü.";
        var sentences = TextChunker.SplitIntoSentences(text);

        sentences.Should().HaveCount(2);
        sentences[0].Should().Be("Dr. Ahmet Bey ve Prof. Mehmet Bey toplantıya katıldı.");
        sentences[1].Should().Be("Konu görüşüldü.");
    }

    [Fact]
    public void SplitIntoChunks_DecimalNumbers_DoesNotSplitAtDecimalPoint()
    {
        var text = "Pi sayısı yaklaşık 3.14 değerindedir. İşlemlerde bu değer kullanılır.";
        var sentences = TextChunker.SplitIntoSentences(text);

        sentences.Should().HaveCount(2);
        sentences[0].Should().Be("Pi sayısı yaklaşık 3.14 değerindedir.");
        sentences[1].Should().Be("İşlemlerde bu değer kullanılır.");
    }

    [Fact]
    public void SplitIntoChunks_Exceeds3000Chars_SplitsIntoMultipleChunksUnderLimit()
    {
        // 3500 karakterlik metin üret
        var baseSentence = "Bu bir yapay zeka ve seslendirme asistanı test cümlesidir. ";
        var sb = new System.Text.StringBuilder();
        while (sb.Length < 3500)
        {
            sb.Append(baseSentence);
        }

        var text = sb.ToString();
        var chunks = TextChunker.SplitIntoChunks(text, 3000);

        chunks.Count.Should().BeGreaterThan(1);
        foreach (var chunk in chunks)
        {
            chunk.Length.Should().BeLessThanOrEqualTo(3000);
        }
    }

    [Fact]
    public void SplitIntoChunks_NeverSplitsInMiddleOfWord()
    {
        var words = "Fırat Üniversitesi Mühendislik Fakültesi Bilgisayar Mühendisliği Tasarım Projesi";
        var chunks = TextChunker.SplitIntoChunks(words, 30);

        foreach (var chunk in chunks)
        {
            // Hiçbir parça kelimenin ortasında kesilmemeli
            var chunkWords = chunk.Split(' ');
            foreach (var w in chunkWords)
            {
                words.Should().Contain(w);
            }
        }
    }

    [Fact]
    public void SplitIntoChunks_MultiplePunctuationMarks_HandledCorrectly()
    {
        var text = "Gerçekten mi?! Bu harika bir haber... Yarın görüşürüz.";
        var sentences = TextChunker.SplitIntoSentences(text);

        sentences.Should().HaveCount(3);
        sentences[0].Should().Be("Gerçekten mi?!");
        sentences[1].Should().Be("Bu harika bir haber...");
        sentences[2].Should().Be("Yarın görüşürüz.");
    }

    [Fact]
    public void SplitIntoChunks_MultipleParagraphs_PreservesSentenceFlow()
    {
        var text = "Paragraf bir burası.\n\nİkinci paragraf burasıdır.";
        var sentences = TextChunker.SplitIntoSentences(text);

        sentences.Should().HaveCount(2);
        sentences[0].Should().Be("Paragraf bir burası.");
        sentences[1].Should().Be("İkinci paragraf burasıdır.");
    }
}
