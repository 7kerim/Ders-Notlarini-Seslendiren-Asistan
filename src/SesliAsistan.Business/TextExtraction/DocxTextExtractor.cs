using System.Text;
using DocumentFormat.OpenXml.Packaging;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.Entities.Enums;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SesliAsistan.Business.TextExtraction;

/// <summary>OpenXML SDK ile .docx dosyalarından başlık stillerini koruyarak metin çıkarır.</summary>
public class DocxTextExtractor : ITextExtractor
{
    public DocumentFileType SupportedType => DocumentFileType.Docx;

    public async Task<ExtractionResult> ExtractAsync(Stream content, CancellationToken ct = default)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        using var doc = WordprocessingDocument.Open(buffer, false);
        var body = doc.MainDocumentPart?.Document?.Body
                   ?? throw new InvalidOperationException("Word belgesinin gövdesi okunamadı.");

        var blocks = new List<TextBlock>();
        var full = new StringBuilder();

        foreach (var paragraph in body.Descendants<W.Paragraph>())
        {
            var text = paragraph.InnerText.Trim();
            if (text.Length == 0) continue;
            full.AppendLine(text);

            var props = paragraph.ParagraphProperties;
            var level = HeadingDetector.LevelFromWordStyle(props?.ParagraphStyleId?.Val?.Value);

            // Doğrudan paragrafa atanmış anahat düzeyi (0 tabanlı)
            if (level is null && props?.OutlineLevel?.Val?.Value is int outline && outline < 6)
                level = outline + 1;

            // Stil kullanılmamışsa numaralı başlık sezgisi
            if (level is null && HeadingDetector.TryDetectNumbered(text, out var numbered))
                level = numbered;

            blocks.Add(new TextBlock(text, level));
        }

        if (full.Length == 0)
            throw new InvalidOperationException("Belge boş görünüyor; metin bulunamadı.");

        return new ExtractionResult(full.ToString().Trim(), null, SectionBuilder.Build(blocks));
    }
}
