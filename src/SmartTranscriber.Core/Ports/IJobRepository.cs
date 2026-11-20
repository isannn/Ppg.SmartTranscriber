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

    /// <summary>
    /// Atomically inserts <paramref name="job"/> owning its
    /// <c>SourceUrl</c>, or returns the live (non-Failed) job already
    /// owning that URL. <c>Created</c> is false when an existing owner
    /// was found — the caller then decides clone-vs-return.
    /// </summary>
    Task<(TranscriptionJob Job, bool Created)> GetOrAddUrlJobAsync(TranscriptionJob job, CancellationToken ct = default);

    /// <summary>Newest Done job with the same content hash (excluding <paramref name="excludeId"/>).</summary>
    Task<TranscriptionJob?> FindDoneByContentHashAsync(string contentHash, Guid excludeId, CancellationToken ct = default);

    /// <summary>Other jobs still referencing the same storage object.</summary>
    Task<int> CountByObjectNameAsync(string objectName, Guid excludeId, CancellationToken ct = default);

    /// <summary>Live (non-Failed) job owning <paramref name="sourceUrl"/>, excluding <paramref name="excludeId"/>.</summary>
    Task<TranscriptionJob?> FindUrlOwnerAsync(string sourceUrl, Guid excludeId, CancellationToken ct = default);
    Task UpdateAsync(TranscriptionJob job, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<JobStatsDto> GetStatsAsync(CancellationToken ct = default);
}
