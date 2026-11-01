using System.Text;
using System.Text.RegularExpressions;

namespace SesliAsistan.Business.Speech;

public static class TextChunker
{
    public const int DefaultMaxChunkLength = 3000;

    // Türkçe yaygın kısaltmalar (nokta ile biten ama cümle sonlandırmayan)
    private static readonly HashSet<string> TurkishAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "dr", "prof", "doc", "doç", "uzm", "av", "arş", "gör", "yrd", "vb", "vs", "örn", "orn",
        "sf", "s", "no", "cad", "sok", "mah", "apt", "bkz", "hz", "tc", "m.ö", "m.s", "y.y"
    };

    /// <summary>
    /// Metni cümle sınırlarından parçalayarak belirlenen karakter sınırını aşmayan parçalara ayırır.
    /// Kısaltmaları ve ondalık sayıları cümle sonu saymaz.
    /// </summary>
    public static List<string> SplitIntoChunks(string? text, int maxChunkLength = DefaultMaxChunkLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var sentences = SplitIntoSentences(text.Trim());
        if (sentences.Count == 0)
            return [];

        var chunks = new List<string>();
        var currentChunk = new StringBuilder();

        foreach (var sentence in sentences)
        {
            // Eğer tek bir cümle bile maxChunkLength'ten büyükse, o cümleyi kelime sınırlarından böl
            if (sentence.Length > maxChunkLength)
            {
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                }

                var subChunks = SplitByWords(sentence, maxChunkLength);
                foreach (var sc in subChunks)
                {
                    chunks.Add(sc.Trim());
                }
                continue;
            }

            // Mevcut parçaya eklersek sınırı aşıyor mu?
            if (currentChunk.Length > 0 && (currentChunk.Length + 1 + sentence.Length) > maxChunkLength)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
            }

            if (currentChunk.Length > 0)
                currentChunk.Append(' ');
            currentChunk.Append(sentence);
        }

        if (currentChunk.Length > 0)
            chunks.Add(currentChunk.ToString().Trim());

        return chunks;
    }

    /// <summary>
    /// Metni cümlelere ayırır. Kısaltmalar ve ondalık sayılar istisna tutulur.
    /// </summary>
    public static List<string> SplitIntoSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var sentences = new List<string>();
        var currentSentence = new StringBuilder();
        var len = text.Length;

        for (int i = 0; i < len; i++)
        {
            char c = text[i];
            currentSentence.Append(c);

            // Cümle bitiş karakteri mi?
            if (c == '.' || c == '!' || c == '?')
            {
                // Çoklu noktalama varsa (..., !?, vb.) hepsini mevcut cümleye dahil et
                while (i + 1 < len && (text[i + 1] == '.' || text[i + 1] == '!' || text[i + 1] == '?'))
                {
                    i++;
                    currentSentence.Append(text[i]);
                }

                // Noktadan hemen önce ondalık sayı var mı? (Örn: "3.14")
                if (c == '.' && i + 1 < len && char.IsDigit(text[i + 1]))
                {
                    int prevIdx = i - 1;
                    while (prevIdx >= 0 && char.IsWhiteSpace(text[prevIdx])) prevIdx--;
                    if (prevIdx >= 0 && char.IsDigit(text[prevIdx]))
                    {
                        // Ondalık sayı noktası, cümle sonu değil
                        continue;
                    }
                }

                // Kısaltma kontrolü (örn. "Dr.", "vb.")
                if (c == '.')
                {
                    var lastWord = GetLastWord(currentSentence.ToString(), currentSentence.Length - 1);
                    if (!string.IsNullOrEmpty(lastWord) && TurkishAbbreviations.Contains(lastWord))
                    {
                        // Kısaltma noktası, cümle sonu değil
                        continue;
                    }
                }

                // Cümle sonu onaylandı
                // Eğer bir sonraki karakter boşluk veya satır sonu ise ya da metin bitmişse
                if (i + 1 >= len || char.IsWhiteSpace(text[i + 1]))
                {
                    var s = currentSentence.ToString().Trim();
                    if (!string.IsNullOrEmpty(s))
                    {
                        sentences.Add(s);
                        currentSentence.Clear();
                    }
                }
            }
        }

        var remaining = currentSentence.ToString().Trim();
        if (!string.IsNullOrEmpty(remaining))
            sentences.Add(remaining);

        return sentences;
    }

    private static string GetLastWord(string text, int dotIndex)
    {
        int end = dotIndex - 1;
        while (end >= 0 && char.IsWhiteSpace(text[end])) end--;
        if (end < 0) return string.Empty;

        int start = end;
        while (start >= 0 && (char.IsLetter(text[start]) || text[start] == '.')) start--;
        start++;

        return text.Substring(start, end - start + 1).Trim('.');
    }

    private static List<string> SplitByWords(string longSentence, int maxLength)
    {
        var words = longSentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length + 1 + word.Length > maxLength)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }

                // Tek bir kelime bile maxLength'ten büyükse zorunlu karakter bölmesi yap
                if (word.Length > maxLength)
                {
                    for (int i = 0; i < word.Length; i += maxLength)
                    {
                        result.Add(word.Substring(i, Math.Min(maxLength, word.Length - i)));
                    }
                    continue;
                }
            }

            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        return result;
    }
}
