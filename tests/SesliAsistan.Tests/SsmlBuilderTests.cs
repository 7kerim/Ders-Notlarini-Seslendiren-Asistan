using FluentAssertions;
using SesliAsistan.Business.Speech;

namespace SesliAsistan.Tests;

public class SsmlBuilderTests
{
    [Fact]
    public void BuildSsml_EscapesXmlSpecialCharacters()
    {
        var input = "Algoritmalar & Veri Yapıları <Ders Notu> \"Bölüm 1\" 'Özet'";
        var ssml = SsmlBuilder.BuildSsml(input);

        ssml.Should().Contain("&amp;");
        ssml.Should().Contain("&lt;Ders Notu&gt;");
        ssml.Should().Contain("&quot;Bölüm 1&quot;");
        ssml.Should().Contain("&apos;Özet&apos;");
    }

    [Fact]
    public void BuildSsml_ConvertsSpeedToProsodyRatePercentage()
    {
        var ssml1 = SsmlBuilder.BuildSsml("Test", speed: 1.0m);
        ssml1.Should().Contain("rate=\"+0%\"");

        var ssmlFast = SsmlBuilder.BuildSsml("Test", speed: 1.5m);
        ssmlFast.Should().Contain("rate=\"+50%\"");

        var ssmlSlow = SsmlBuilder.BuildSsml("Test", speed: 0.8m);
        ssmlSlow.Should().Contain("rate=\"-20%\"");
    }

    [Fact]
    public void BuildSsml_SetsCorrectVoiceNameAndLanguageTag()
    {
        var ssml = SsmlBuilder.BuildSsml("Metin", voice: "tr-TR-AhmetNeural");

        ssml.Should().Contain("xml:lang=\"tr-TR\"");
        ssml.Should().Contain("<voice name=\"tr-TR-AhmetNeural\">");
    }

    [Fact]
    public void BuildSsml_WithBreakAfterTitle_AppendsBreakTag()
    {
        var ssml = SsmlBuilder.BuildSsml("Başlık 1", addBreakAfterTitle: true);

        ssml.Should().Contain("<break time=\"750ms\"/>");
    }

    [Fact]
    public void BuildSsml_ClampsSpeedWithinValidRange()
    {
        var ssmlTooSlow = SsmlBuilder.BuildSsml("Test", speed: 0.1m);
        ssmlTooSlow.Should().Contain("rate=\"-50%\""); // en az 0.5m

        var ssmlTooFast = SsmlBuilder.BuildSsml("Test", speed: 3.5m);
        ssmlTooFast.Should().Contain("rate=\"+100%\""); // en fazla 2.0m
    }
}
