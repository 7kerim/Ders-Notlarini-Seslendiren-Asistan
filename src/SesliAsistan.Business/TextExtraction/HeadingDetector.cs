using System.Text.RegularExpressions;

namespace SesliAsistan.Business.TextExtraction;

/// <summary>
/// Stil bilgisi olmayan metinlerde (PDF) başlıkları sezgisel olarak tespit eder.
/// Örnekler: "1. GİRİŞ" → 1, "2.3 Veri Modeli" → 2, "3.1.2) Testler" → 3.
/// </summary>
public static partial class HeadingDetector
{
    private const int MaxHeadingLength = 90;

    [GeneratedRegex(@"^(?<num>\d{1,2}(?:\.\d{1,2}){0,3})[\.\)]?\s+(?<title>\S.*)$")]
    private static partial Regex NumberedHeading();

    [GeneratedRegex(@"^(Heading|Başlık|Baslik|Balk)\s?(?<lvl>[1-6])$", RegexOptions.IgnoreCase)]
    private static partial Regex WordHeadingStyle();

    public static bool TryDetectNumbered(string line, out int level)
    {
        level = 0;
        var text = line.Trim();
        if (text.Length == 0 || text.Length > MaxHeadingLength) return false;
        // Cümle gibi biten satırlar (nokta ile biten uzun satırlar) başlık sayılmaz
        if (text.EndsWith('.') && text.Length > 40) return false;

        var match = NumberedHeading().Match(text);
        if (!match.Success) return false;

        var title = match.Groups["title"].Value;
        // Başlık harfle başlamalı ("1. 250 ml su" gibi listeleri ele)
        if (!char.IsLetter(title[0])) return false;

        level = match.Groups["num"].Value.Split('.', StringSplitOptions.RemoveEmptyEntries).Length;
        return true;
    }

    /// <summary>Word paragraf stil kimliğinden (Heading1, Balk2 vb.) başlık seviyesini çıkarır.</summary>
    public static int? LevelFromWordStyle(string? styleId)
    {
        if (string.IsNullOrWhiteSpace(styleId)) return null;
        if (styleId.Equals("Title", StringComparison.OrdinalIgnoreCase) ||
            styleId.Equals("KonuBal", StringComparison.OrdinalIgnoreCase)) return 1;

        var match = WordHeadingStyle().Match(styleId);
        return match.Success ? int.Parse(match.Groups["lvl"].Value) : null;
    }
}
