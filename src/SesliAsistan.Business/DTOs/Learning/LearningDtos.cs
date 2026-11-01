using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Business.DTOs.Learning;

public record VoiceDto(string Id, string Name, string Gender, string Locale);

public record AudioResultDto(
    Guid AudioId,
    string Url,
    string Voice,
    decimal Speed,
    double DurationSeconds,
    bool FromCache);

public record SaveProgressRequest(Guid? SectionId, int PositionSeconds);

public record ListeningProgressDto(
    Guid DocumentId,
    Guid? SectionId,
    int PositionSeconds,
    DateTime UpdatedAt);

public record CreateSummaryRequest(SummaryLength Length = SummaryLength.Medium);

public record SummaryDto(
    Guid Id,
    Guid DocumentId,
    Guid? SectionId,
    SummaryLength Length,
    string Content,
    DateTime CreatedAt);
