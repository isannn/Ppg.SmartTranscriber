using SmartTranscriber.Core.Domain;

namespace SmartTranscriber.Core.Dto;

public sealed record JobDto(
    Guid Id,
    string ObjectName,
    string OriginalFileName,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    string? Model,
    string? Language,
    double? DurationSeconds,
    string? Summary,
    int Progress,
    int SegmentCount);

public sealed record SegmentDto(
    long Id,
    double StartTime,
    double EndTime,
    string? Speaker,
    string Text);

public sealed record SpeakerUpdateRequest(string? Speaker);

public sealed record TranscriptResult(
    IReadOnlyList<TranscriptSegment> Segments,
    string? Model,
    double? DurationSeconds,
    string? Language);

public sealed record SummaryRequest(string? Model = null, string? Prompt = null, bool Regenerate = false);

public sealed record SearchFilters(
    Guid? JobId = null,
    string? Speaker = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Model = null);

public sealed record SearchHitDto(
    long SegmentId,
    Guid JobId,
    string OriginalFileName,
    double StartTime,
    double EndTime,
    string? Speaker,
    string Snippet);
