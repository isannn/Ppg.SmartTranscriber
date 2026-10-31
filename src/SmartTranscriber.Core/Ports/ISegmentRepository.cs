using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;

namespace SmartTranscriber.Core.Ports;

public interface ISegmentRepository
{
    Task<IReadOnlyList<TranscriptSegment>> GetByJobAsync(Guid jobId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<TranscriptSegment> segments, CancellationToken ct = default);
    Task<bool> UpdateSpeakerAsync(long segmentId, string? speaker, CancellationToken ct = default);
    /// <summary>Bulk speaker rename across one job; returns affected rows.</summary>
    Task<int> RenameSpeakerAsync(Guid jobId, string from, string to, CancellationToken ct = default);
    Task<IReadOnlyList<SearchHitDto>> SearchAsync(string query, int limit, SearchFilters? filters = null, CancellationToken ct = default);
}
