using SmartTranscriber.Core.Dto;

namespace SmartTranscriber.Core.Services;

public interface ITranscriptService
{
    Task<IReadOnlyList<SegmentDto>> GetSegmentsAsync(Guid jobId, CancellationToken ct = default);
    Task<bool> UpdateSpeakerAsync(long segmentId, string? speaker, CancellationToken ct = default);
    Task<string?> RenderTranscriptAsync(Guid jobId, CancellationToken ct = default);
    Task<string?> RenderTranscriptAsync(Guid jobId, Ports.TranscriptFormat format, CancellationToken ct = default);
    Task<(string Summary, bool Cached)?> GetOrCreateSummaryAsync(Guid jobId, bool regenerate, CancellationToken ct = default);
    Task<IReadOnlyList<SearchHitDto>> SearchAsync(string query, int limit, CancellationToken ct = default);
}
