using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;

namespace SmartTranscriber.Core.Services;

public interface IJobService
{
    Task<JobDto> CreateJobAsync(string fileName, Stream content, string contentType, string? callbackUrl = null, string? hotwords = null, int priority = 0, CancellationToken ct = default);
    /// <summary>Creates a Downloading job and ingests media from a URL in the background.</summary>
    Task<JobDto> CreateUrlJobAsync(string url, string? callbackUrl = null, string? hotwords = null, int priority = 0, CancellationToken ct = default);
    Task<JobDto?> GetJobAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<JobDto>> ListJobsAsync(JobStatus? status, string? tag, int limit, int offset, CancellationToken ct = default);
    Task<bool> UpdateTagsAsync(Guid id, string tags, CancellationToken ct = default);
    Task<bool> DeleteJobAsync(Guid id, bool deleteFile, CancellationToken ct = default);
    Task<(Stream Stream, string ObjectName)?> DownloadMediaAsync(Guid id, CancellationToken ct = default);
    /// <summary>
    /// Requeues a Failed or Cancelled job. Jobs that failed during URL
    /// ingest are sent back through the download instead (no media was
    /// ever stored).
    /// </summary>
    Task<RetryOutcome> RetryJobAsync(Guid id, CancellationToken ct = default);
    /// <summary>
    /// Marks a live job Cancelled and signals in-flight work to stop.
    /// Null = unknown id, false = already in a terminal status.
    /// </summary>
    Task<bool?> CancelJobAsync(Guid id, CancellationToken ct = default);
    Task<JobDto?> RetranscribeAsync(Guid id, string? model, string? language, bool? diarize, string? hotwords = null, CancellationToken ct = default);
    Task<IReadOnlyList<(Guid Id, bool Ok, string Detail)>> BulkAsync(string action, IReadOnlyList<Guid> ids, CancellationToken ct = default);
    Task<JobStatsDto> GetStatsAsync(CancellationToken ct = default);
    Task<CompareDto?> CompareAsync(Guid a, Guid b, CancellationToken ct = default);
}
