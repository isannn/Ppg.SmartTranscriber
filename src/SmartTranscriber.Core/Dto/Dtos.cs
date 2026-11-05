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
    int SegmentCount,
    string? CallbackUrl = null,
    int Priority = 0,
    string? Tags = null,
    string? Translation = null,
    string? TranslationLang = null);

public sealed record SegmentDto(
    long Id,
    double StartTime,
    double EndTime,
    string? Speaker,
    string Text,
    double? Confidence = null);

public sealed record SpeakerUpdateRequest(string? Speaker);
public sealed record TextUpdateRequest(string Text);

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

public sealed record JobStatsDto(
    int Jobs,
    Dictionary<string, int> ByStatus,
    IReadOnlyList<JobStatsModel> ByModel,
    IReadOnlyList<JobStatsDay> ByDay,
    double TotalDurationSeconds);
public sealed record JobStatsModel(string? Model, int Jobs, double AvgDurationSeconds);
public sealed record JobStatsDay(string Date, int Jobs);

public sealed record CompareRow(int Index, double AStart, double BStart, string AText, string BText, bool Same);
public sealed record CompareDto(JobDto JobA, JobDto JobB, IReadOnlyList<CompareRow> Rows);

public sealed record SearchHitDto(
    string Type,
    long SegmentId,
    Guid JobId,
    string OriginalFileName,
    double StartTime,
    double EndTime,
    string? Speaker,
    string Snippet);
