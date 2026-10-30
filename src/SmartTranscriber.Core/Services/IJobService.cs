using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;

namespace SmartTranscriber.Core.Services;

public interface IJobService
{
    Task<JobDto> CreateJobAsync(string fileName, Stream content, string contentType, CancellationToken ct = default);
    /// <summary>Creates a Downloading job and ingests media from a URL in the background.</summary>
    Task<JobDto> CreateUrlJobAsync(string url, CancellationToken ct = default);
    Task<JobDto?> GetJobAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<JobDto>> ListJobsAsync(JobStatus? status, int limit, int offset, CancellationToken ct = default);
    Task<bool> DeleteJobAsync(Guid id, bool deleteFile, CancellationToken ct = default);
    Task<(Stream Stream, string ObjectName)?> DownloadMediaAsync(Guid id, CancellationToken ct = default);
}
