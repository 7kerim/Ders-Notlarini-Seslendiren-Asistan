using FluentAssertions;
using SesliAsistan.Business.TextExtraction;

namespace SesliAsistan.Tests;

public class TextExtractionTests
{
    [Theory]
    [InlineData("1. GİRİŞ", 1)]
    [InlineData("2.3 Veri Modeli", 2)]
    [InlineData("3.1.2) Birim Testleri", 3)]
    public void Numbered_Headings_AreDetected(string line, int expectedLevel)
    {
        HeadingDetector.TryDetectNumbered(line, out var level).Should().BeTrue();
        level.Should().Be(expectedLevel);
    }

    [Theory]
    [InlineData("Bu bir paragraf cümlesidir ve başlık değildir.")]
    [InlineData("1. 250 ml su eklenir")]
    [InlineData("")]
    public void Plain_Text_IsNotHeading(string line) =>
        HeadingDetector.TryDetectNumbered(line, out _).Should().BeFalse();

    [Theory]
    [InlineData("Heading1", 1)]
    [InlineData("Heading3", 3)]
    [InlineData("Balk2", 2)]   // Türkçe Word'de "Başlık 2" stil kimliği
    [InlineData("Normal", null)]
    public void WordStyles_MapToLevels(string styleId, int? expected) =>
        HeadingDetector.LevelFromWordStyle(styleId).Should().Be(expected);

    [Fact]
    public void SectionBuilder_GroupsParagraphsUnderHeadings_AndLinksParents()
    {
        var blocks = new[]
        {
            new TextBlock("Önsöz metni", null),
            new TextBlock("1. Ağlar", 1),
            new TextBlock("Ağlar hakkında giriş.", null),
            new TextBlock("1.1 TCP", 2),
            new TextBlock("TCP güvenilir iletim sağlar.", null),
            new TextBlock("2. Güvenlik", 1)
        };

        var sections = SectionBuilder.Build(blocks);
        sections.Select(s => s.Title).Should().Equal("Giriş", "1. Ağlar", "1.1 TCP", "2. Güvenlik");
        sections[2].Content.Should().Be("TCP güvenilir iletim sağlar.");

        var linked = SectionBuilder.LinkParents(sections);
        linked[2].ParentIndex.Should().Be(1); // "1.1 TCP" → "1. Ağlar"
        linked[3].ParentIndex.Should().BeNull();
    }

    [Fact]
    public void LinkParents_NormalizesSkippedLevels()
    {
        var sections = new[]
        {
            new ExtractedSection("A", 1, ""),
            new ExtractedSection("A.x.y", 3, "") // 1 → 3 atlaması
        };

        var linked = SectionBuilder.LinkParents(sections);
        linked[1].Section.Level.Should().Be(2);
        linked[1].ParentIndex.Should().Be(0);
    }
}
