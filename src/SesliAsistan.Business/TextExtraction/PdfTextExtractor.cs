using System.Text;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.Entities.Enums;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SesliAsistan.Business.TextExtraction;

/// <summary>PdfPig ile metin tabanlı PDF'lerden okuma sırasını koruyarak metin çıkarır.</summary>
public class PdfTextExtractor : ITextExtractor
{
    public DocumentFileType SupportedType => DocumentFileType.Pdf;

    public async Task<ExtractionResult> ExtractAsync(Stream content, CancellationToken ct = default)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        using var pdf = PdfDocument.Open(buffer);
        var blocks = new List<TextBlock>();
        var pageTexts = new List<string>();
        var full = new StringBuilder();

        foreach (var page in pdf.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            var pageText = ContentOrderTextExtractor.GetText(page);
            pageTexts.Add(pageText);
            full.AppendLine(pageText);

            foreach (var raw in pageText.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || IsPageNumber(line)) continue;
                blocks.Add(HeadingDetector.TryDetectNumbered(line, out var level)
                    ? new TextBlock(line, level)
                    : new TextBlock(line, null));
            }
        }

        if (full.ToString().Trim().Length == 0)
            throw new InvalidOperationException(
                "Belgeden metin çıkarılamadı. Taranmış (görüntü) PDF'ler bu sürümde desteklenmemektedir.");

        var sections = blocks.Any(b => b.HeadingLevel is not null)
            ? SectionBuilder.Build(blocks)
            : pageTexts.Select((t, i) => new ExtractedSection($"Sayfa {i + 1}", 1, t.Trim())).ToList();

        return new ExtractionResult(full.ToString().Trim(), pdf.NumberOfPages, sections);
    }

    private static bool IsPageNumber(string line) => line.Length <= 4 && line.All(char.IsDigit);
}
