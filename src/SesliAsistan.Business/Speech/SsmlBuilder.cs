using System.Security;
using System.Text;

namespace SesliAsistan.Business.Speech;

public static class SsmlBuilder
{
    public const string DefaultVoice = "tr-TR-EmelNeural";
    public const string MaleVoice = "tr-TR-AhmetNeural";

    /// <summary>
    /// Metni W3C SSML 1.1 uyumlu XML formatına dönüştürür.
    /// Hız değerini (0.5x - 2.0x) prosody rate yüzdesine çevirir ve XML kaçışlarını uygular.
    /// </summary>
    public static string BuildSsml(string text, string voice = DefaultVoice, decimal speed = 1.0m, bool addBreakAfterTitle = false)
    {
        var safeVoice = string.IsNullOrWhiteSpace(voice) ? DefaultVoice : voice.Trim();
        var safeSpeed = Math.Clamp(speed, 0.5m, 2.0m);

        // Hızı yüzdeye çevir (örn: 1.0 -> +0%, 1.25 -> +25%, 0.75 -> -25%)
        int ratePercent = (int)Math.Round((safeSpeed - 1.0m) * 100);
        string rateStr = ratePercent >= 0 ? $"+{ratePercent}%" : $"{ratePercent}%";

        var escapedText = EscapeXml(text ?? string.Empty);
        if (addBreakAfterTitle)
        {
            escapedText += "<break time=\"750ms\"/>";
        }

        return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"tr-TR\">" +
               $"<voice name=\"{safeVoice}\">" +
               $"<prosody rate=\"{rateStr}\">" +
               $"{escapedText}" +
               $"</prosody>" +
               $"</voice>" +
               $"</speak>";
    }

    /// <summary>
    /// XML özel karakterlerini (&, <, >, ", ') standart entity karşılıklarına dönüştürür.
    /// </summary>
    public static string EscapeXml(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
