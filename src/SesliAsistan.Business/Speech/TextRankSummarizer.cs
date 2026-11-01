using System.Text.RegularExpressions;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Business.Speech;

public class TextRankSummarizer : ITextRankSummarizer
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "acaba", "altı", "ama", "ancak", "arada", "artık", "asla", "aslında", "ayrıca", "az",
        "bana", "bazen", "bazı", "belki", "ben", "benden", "beni", "benim", "beri", "beş",
        "bile", "bin", "bir", "biraz", "biri", "birkaç", "birkez", "birçok", "birşey", "biz",
        "bizden", "bize", "bizi", "bizim", "böyle", "böylece", "bu", "buna", "bunda", "bundan",
        "bunlar", "bunları", "bunların", "bunu", "bunun", "burada", "çok", "çünkü", "da", "daha",
        "dahi", "de", "defa", "değil", "diğer", "diye", "dokuz", "dolayı", "dolayısıyla", "dört",
        "edecek", "eden", "ederek", "edilecek", "ediliyor", "edilmesi", "ediyor", "eğer", "elli",
        "en", "etmesi", "etti", "ettiği", "ettiğini", "gibi", "göre", "halen", "hangi", "hatta",
        "hem", "henüz", "hep", "hepsi", "her", "herhangi", "herkes", "herkesin", "hiç", "hiçbir",
        "için", "iki", "ile", "ilgili", "ise", "işte", "itibaren", "itibariyle", "kadar", "karşın",
        "katrilyon", "kendi", "kendine", "kendini", "kendisi", "kendisine", "kendisini", "kez",
        "ki", "kim", "kimden", "kime", "kimi", "kimse", "kırk", "milyar", "milyon", "mu", "mü",
        "mı", "nasıl", "ne", "neden", "nedenle", "nerde", "nerede", "nereye", "nitekim", "ona",
        "ondan", "onlar", "onlardan", "onlara", "onların", "onlarca", "onu", "onun", "orada",
        "oysa", "oysaki", "öbürü", "önce", "önceden", "ötürü", "öyle", "sekiz", "sen", "senden",
        "sene", "seni", "senin", "siz", "sizden", "size", "sizi", "sizin", "sonra", "sonradan",
        "şayet", "şey", "şeyden", "şeyler", "şimdi", "şöyle", "şu", "şuna", "şunda", "şundan",
        "şunları", "şunu", "tarafından", "trilyon", "tüm", "üç", "üzere", "var", "vardı", "ve",
        "veya", "veyahut", "ya", "yahut", "yalnız", "yani", "yapacak", "yapılan", "yapılması",
        "yapıyor", "yedi", "yerine", "yine", "yok", "yoksa", "yüz", "zaten"
    };

    // Önceki cümleye referans veren bağlam zamir ve kelimeleri
    private static readonly string[] ContextReferencePrefixes =
    [
        "bu ", "şu ", "o ", "bunlar ", "şunlar ", "onlar ", "bunun ", "şunun ", "onun ",
        "böylece ", "bu nedenle ", "bu sebeple ", "yukarıdaki ", "yukarıda belirtilen ", "söz konusu "
    ];

    public string Summarize(string text, SummaryLength length)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sentences = TextChunker.SplitIntoSentences(text.Trim());
        if (sentences.Count <= 1)
            return text.Trim();

        // 1. Cümleleri tokenize et ve durak kelimeleri temizleyip F5 kök kesme uygula
        var processedSentences = new List<HashSet<string>>();
        foreach (var sentence in sentences)
        {
            var tokens = TokenizeAndStem(sentence);
            processedSentences.Add(tokens);
        }

        // 2. Cümle benzerlik matrisini oluştur
        int n = sentences.Count;
        double[,] similarity = new double[n, n];
        double[] degrees = new double[n];

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                double sim = CalculateSimilarity(processedSentences[i], processedSentences[j]);
                similarity[i, j] = sim;
                similarity[j, i] = sim;
                degrees[i] += sim;
                degrees[j] += sim;
            }
        }

        // 3. PageRank iterasyonu (TextRank)
        double[] scores = new double[n];
        for (int i = 0; i < n; i++) scores[i] = 1.0;

        const double d = 0.85; // Damping factor
        const int maxIterations = 30;
        const double threshold = 0.0001;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            double maxDiff = 0.0;
            double[] nextScores = new double[n];

            for (int i = 0; i < n; i++)
            {
                double sum = 0.0;
                for (int j = 0; j < n; j++)
                {
                    if (i != j && degrees[j] > 0.0)
                    {
                        sum += (similarity[j, i] / degrees[j]) * scores[j];
                    }
                }
                nextScores[i] = (1.0 - d) + d * sum;
                double diff = Math.Abs(nextScores[i] - scores[i]);
                if (diff > maxDiff) maxDiff = diff;
            }

            scores = nextScores;
            if (maxDiff < threshold) break;
        }

        // 4. Uzunluk oranına göre seçilecek cümle sayısını belirle
        double ratio = length switch
        {
            SummaryLength.Short => 0.10,
            SummaryLength.Detailed => 0.35,
            _ => 0.20 // Medium
        };

        int targetCount = Math.Max(1, (int)Math.Ceiling(n * ratio));
        targetCount = Math.Min(targetCount, n);

        // En yüksek puanlı cümle indekslerini seç
        var topIndices = scores
            .Select((score, index) => (score, index))
            .OrderByDescending(x => x.score)
            .Take(targetCount)
            .Select(x => x.index)
            .ToHashSet();

        // 5. Akıcılık kuralı: Eğer seçilen cümle gönderme yapan bir ifadeyle ("Bu", "Şu", "Yukarıdaki")
        // başlıyorsa, bağlam kopmaması için önceki cümle de eklenir
        var expandedIndices = new HashSet<int>(topIndices);
        foreach (var idx in topIndices)
        {
            if (idx > 0 && !expandedIndices.Contains(idx - 1))
            {
                var sentenceLower = sentences[idx].Trim().ToLowerInvariant();
                foreach (var prefix in ContextReferencePrefixes)
                {
                    if (sentenceLower.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        expandedIndices.Add(idx - 1);
                        break;
                    }
                }
            }
        }

        // 6. Seçilen cümleleri belgedeki orijinal sırasına göre birleştir
        var summarySentences = expandedIndices
            .OrderBy(idx => idx)
            .Select(idx => sentences[idx])
            .ToList();

        return string.Join(" ", summarySentences);
    }

    /// <summary>
    /// Kelimeleri küçük harfe çevirir, durak kelimeleri eler ve F5 kuralıyla ilk 5 harfine indirger.
    /// </summary>
    public static HashSet<string> TokenizeAndStem(string sentence)
    {
        var words = Regex.Split(sentence.ToLowerInvariant(), @"[^\p{L}0-9]+");
        var stems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var word in words)
        {
            if (word.Length < 2) continue;
            if (StopWords.Contains(word)) continue;

            // F5 kök kesme: ilk 5 karakter
            var stem = word.Length > 5 ? word[..5] : word;
            stems.Add(stem);
        }

        return stems;
    }

    private static double CalculateSimilarity(HashSet<string> s1, HashSet<string> s2)
    {
        if (s1.Count == 0 || s2.Count == 0) return 0.0;

        int intersection = 0;
        foreach (var w in s1)
        {
            if (s2.Contains(w)) intersection++;
        }

        if (intersection == 0) return 0.0;

        // Normalizasyon: log(|s1|) + log(|s2|)
        double norm = Math.Log(s1.Count) + Math.Log(s2.Count);
        return norm > 0 ? intersection / norm : 0.0;
    }
}
