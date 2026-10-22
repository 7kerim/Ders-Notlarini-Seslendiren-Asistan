using System.Text;

namespace SesliAsistan.Business.TextExtraction;

/// <summary>Düz blok listesini başlık + içerik bölümlerine dönüştürür.</summary>
public static class SectionBuilder
{
    public const string IntroTitle = "Giriş";

    public static IReadOnlyList<ExtractedSection> Build(IEnumerable<TextBlock> blocks)
    {
        var sections = new List<(string Title, int Level, StringBuilder Content)>();

        foreach (var block in blocks)
        {
            var text = block.Text.Trim();
            if (text.Length == 0) continue;

            if (block.HeadingLevel is int level)
            {
                sections.Add((text, Math.Clamp(level, 1, 6), new StringBuilder()));
                continue;
            }

            // Belge başında başlıksız metin varsa "Giriş" bölümü aç
            if (sections.Count == 0) sections.Add((IntroTitle, 1, new StringBuilder()));

            var current = sections[^1].Content;
            if (current.Length > 0) current.AppendLine();
            current.Append(text);
        }

        return sections
            .Select(s => new ExtractedSection(s.Title, s.Level, s.Content.ToString()))
            .ToList();
    }

    /// <summary>
    /// Seviyeleri ardışık olmayan başlıkları (ör. 1 → 3) düzeltir; böylece ağaçta boş ara düğüm oluşmaz.
    /// Dönen liste: (bölüm, ebeveyn indeksi veya null).
    /// </summary>
    public static IReadOnlyList<(ExtractedSection Section, int? ParentIndex)> LinkParents(IReadOnlyList<ExtractedSection> sections)
    {
        var result = new List<(ExtractedSection, int?)>(sections.Count);
        var stack = new Stack<(int Index, int Level)>();

        for (var i = 0; i < sections.Count; i++)
        {
            var level = sections[i].Level;
            while (stack.Count > 0 && stack.Peek().Level >= level) stack.Pop();

            int? parent = stack.Count > 0 ? stack.Peek().Index : null;
            var normalizedLevel = stack.Count + 1;
            result.Add((sections[i] with { Level = normalizedLevel }, parent));
            stack.Push((i, level));
        }

        return result;
    }
}
