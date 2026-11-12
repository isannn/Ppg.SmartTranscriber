using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;

namespace SmartTranscriber.Core.Ports;

public interface IJobRepository
{
    Task<TranscriptionJob?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TranscriptionJob>> ListAsync(JobStatus? status, string? tag, int limit, int offset, CancellationToken ct = default);
    Task<TranscriptionJob?> ClaimNextPendingAsync(CancellationToken ct = default);

    /// <summary>
    /// Startup recovery for jobs orphaned by a crash/restart:
    /// Processing → Pending (media is already in S3, pipeline re-downloads),
    /// Downloading → Failed (no media was ever stored, ingest cannot resume).
    /// Returns the transitioned jobs.
    /// </summary>
    Task<IReadOnlyList<TranscriptionJob>> ResetInterruptedJobsAsync(CancellationToken ct = default);
    Task AddAsync(TranscriptionJob job, CancellationToken ct = default);
    Task UpdateAsync(TranscriptionJob job, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<JobStatsDto> GetStatsAsync(CancellationToken ct = default);
}
