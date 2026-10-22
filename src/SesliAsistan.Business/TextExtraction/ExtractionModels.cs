namespace SesliAsistan.Business.TextExtraction;

public record ExtractedSection(string Title, int Level, string Content);

public record ExtractionResult(string FullText, int? PageCount, IReadOnlyList<ExtractedSection> Sections);

/// <summary>Metin bloğu: başlık ise HeadingLevel dolu, paragraf ise null.</summary>
public record TextBlock(string Text, int? HeadingLevel);
