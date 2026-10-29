using Microsoft.Extensions.Logging;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Core.Services;

public sealed class JobService(
    IJobRepository jobs,
    IStorageService storage,
    IJobQueue queue,
    ILogger<JobService> logger) : IJobService
{
    public async Task<JobDto> CreateJobAsync(string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var objectName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";

        await storage.EnsureBucketAsync(ct);
        await storage.UploadAsync(objectName, content, contentType, ct);

        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = objectName,
            OriginalFileName = Path.GetFileName(fileName),
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await jobs.AddAsync(job, ct);
        await queue.EnqueueAsync(job.Id, ct);

        logger.LogInformation("Created transcription job {JobId} for {FileName}", job.Id, fileName);
        return Map(job);
    }

    public async Task<JobDto?> GetJobAsync(Guid id, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(id, ct);
        return job is null ? null : Map(job);
    }

    public async Task<IReadOnlyList<JobDto>> ListJobsAsync(JobStatus? status, int limit, int offset, CancellationToken ct = default)
    {
        var list = await jobs.ListAsync(status, limit, offset, ct);
        return list.Select(Map).ToList();
    }

    public async Task<bool> DeleteJobAsync(Guid id, bool deleteFile, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(id, ct);
        if (job is null) return false;

        if (deleteFile)
        {
            try { await storage.DeleteAsync(job.ObjectName, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to delete object {ObjectName}", job.ObjectName); }
        }

        await jobs.DeleteAsync(id, ct);
        return true;
    }

    public async Task<(Stream Stream, string ObjectName)?> DownloadMediaAsync(Guid id, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(id, ct);
        if (job is null)
        {
            return null;
        }
        var stream = await storage.DownloadAsync(job.ObjectName, ct);
        return (stream, job.ObjectName);
    }

    internal static JobDto Map(TranscriptionJob j) => new(
        j.Id, j.ObjectName, j.OriginalFileName, j.Status.ToString(),
        j.CreatedAt, j.CompletedAt, j.ErrorMessage, j.Model, j.Language,
        j.DurationSeconds, j.Summary, j.SegmentCount > 0 ? j.SegmentCount : j.Segments.Count);
}
