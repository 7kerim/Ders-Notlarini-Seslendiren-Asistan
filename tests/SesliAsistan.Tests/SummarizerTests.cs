using FluentAssertions;
using SesliAsistan.Business.Speech;
using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Tests;

public class SummarizerTests
{
    private static readonly string MultiSentenceArticle =
        "Bilgisayar mühendisliği yazılım ve donanım sistemlerinin tasarımını inceleyen temel bir mühendislik dalıdır. " +
        "Yazılım geliştirme süreçleri analiz tasarım kodlama ve test aşamalarından meydana gelir. " +
        "Veritabanı yönetim sistemleri verilerin güvenli ve tutarlı saklanmasını temin eder. " +
        "İlişkisel veritabanlarında SQL dili kullanılarak sorgulamalar ve veri manipülasyonları gerçekleştirilir. " +
        "Doğal dil işleme insan dilinin bilgisayarlar tarafından algılanıp işlenmesini amaçlar. " +
        "Metin özetleme algoritmaları uzun akademik metinleri ana fikirlerine dönüştürür. " +
        "Bu yöntemler öğrencilerin sınavlara hazırlanırken zamandan tasarruf etmesini sağlar. " +
        "Ses sentezleme teknolojisi ise yazılı metinleri işitsel biçime çevirir. " +
        "Mobil uygulamalar her an her yerde öğrenme imkanı sunarak erişilebilirliği artırır. " +
        "Sonuç olarak yapay zeka destekli eğitim araçları modern pedagojinin ayrılmaz bir parçasıdır.";

    [Fact]
    public void Summarize_SingleSentence_ReturnsSameSentence()
    {
        var summarizer = new TextRankSummarizer();
        var single = "Yapay zeka günümüz dünyasını köklü şekilde dönüştürmektedir.";

        var summary = summarizer.Summarize(single, SummaryLength.Medium);

        summary.Should().Be(single);
    }

    [Fact]
    public void Summarize_LengthRatios_RespectsTargetProportions()
    {
        var summarizer = new TextRankSummarizer();

        var shortSummary = summarizer.Summarize(MultiSentenceArticle, SummaryLength.Short);
        var detailedSummary = summarizer.Summarize(MultiSentenceArticle, SummaryLength.Detailed);

        shortSummary.Length.Should().BeLessThan(detailedSummary.Length);
    }

    [Fact]
    public void Summarize_PreservesOriginalSentenceOrder()
    {
        var summarizer = new TextRankSummarizer();
        var summary = summarizer.Summarize(MultiSentenceArticle, SummaryLength.Medium);

        var summarySentences = TextChunker.SplitIntoSentences(summary);
        summarySentences.Count.Should().BeGreaterThan(1);

        int lastIndex = -1;
        foreach (var s in summarySentences)
        {
            int currentIndex = MultiSentenceArticle.IndexOf(s, StringComparison.Ordinal);
            currentIndex.Should().BeGreaterThan(lastIndex);
            lastIndex = currentIndex;
        }
    }

    [Fact]
    public void Summarize_ContextReferenceSentence_IncludesPrecedingSentence()
    {
        var text = "Veritabanı optimizasyonu sistem performansı için çok önemlidir. " +
                   "Bu algoritmalar veritabanı optimizasyonu ve yapay zeka alanında kullanılır. " +
                   "Yapay zeka sistemleri modern dünyada hızla gelişmektedir. " +
                   "Gelecekte veri odaklı yeni algoritmalar geliştirilecektir.";
        var summarizer = new TextRankSummarizer();

        var summary = summarizer.Summarize(text, SummaryLength.Short);

        // "Bu" ile başlayan merkez cümle seçildiğinde önceki cümle de bağlam için dahil edilir
        summary.Should().Contain("Veritabanı optimizasyonu sistem performansı için çok önemlidir.");
        summary.Should().Contain("Bu algoritmalar veritabanı optimizasyonu ve yapay zeka alanında kullanılır.");
    }

    [Fact]
    public void TokenizeAndStem_RemovesStopWordsAndAppliesF5Stemming()
    {
        var sentence = "ve çünkü bu algoritmalar veritabanı";
        var stems = TextRankSummarizer.TokenizeAndStem(sentence);

        // "ve", "çünkü", "bu" stop-word olarak elenmeli
        stems.Should().NotContain("ve");
        stems.Should().NotContain("çünkü");
        stems.Should().NotContain("bu");

        // "algoritmalar" -> "algor" (ilk 5 harf F5 kök kesme)
        stems.Should().Contain("algor");
        // "veritabanı" -> "verit"
        stems.Should().Contain("verit");
    }

    [Fact]
    public void Summarize_EmptyOrWhitespace_ReturnsEmpty()
    {
        var summarizer = new TextRankSummarizer();

        summarizer.Summarize("", SummaryLength.Medium).Should().BeEmpty();
        summarizer.Summarize("   ", SummaryLength.Medium).Should().BeEmpty();
    }
}
