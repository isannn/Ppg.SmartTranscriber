using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Core.Services;

public sealed class JobService(
    IJobRepository jobs,
    IStorageService storage,
    IJobQueue queue,
    IServiceProvider services,
    ISegmentRepository segments,
    IWebhookNotifier webhooks,
    ILogger<JobService> logger) : IJobService
{
    public async Task<JobDto> CreateJobAsync(string fileName, Stream content, string contentType, string? callbackUrl = null, CancellationToken ct = default)
    {
        var objectName = $"{Guid.NewGuid():N}_{SafeName(Path.GetFileName(fileName))}";

        await storage.EnsureBucketAsync(ct);
        await storage.UploadAsync(objectName, content, contentType, ct);

        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = objectName,
            OriginalFileName = Path.GetFileName(fileName),
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CallbackUrl = callbackUrl
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

    public async Task<JobDto> CreateUrlJobAsync(string url, string? callbackUrl = null, CancellationToken ct = default)
    {
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = $"url/{Guid.NewGuid():N}",
            OriginalFileName = url,
            Status = JobStatus.Downloading,
            Progress = 2,
            CreatedAt = DateTime.UtcNow,
            CallbackUrl = callbackUrl
        };
        await jobs.AddAsync(job, ct);
        _ = Task.Run(() => IngestUrlAsync(job.Id, url));
        return Map(job);
    }

    /// <summary>Background: fetch media, upload to S3, mark Pending+enqueue or Failed.</summary>
    private async Task IngestUrlAsync(Guid jobId, string url)
    {
        try
        {
            using var scope = services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            var store = scope.ServiceProvider.GetRequiredService<IStorageService>();
            var fetcher = scope.ServiceProvider.GetRequiredService<IMediaFetcher>();
            var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();

            var job = await repo.GetByIdAsync(jobId) ?? throw new InvalidOperationException("job gone");
            var dir = Path.Combine(Path.GetTempPath(), $"st-yt-{jobId:N}");
            Directory.CreateDirectory(dir);
            try
            {
                var file = await fetcher.FetchAsync(url, dir);
                var objectName = $"{jobId:N}_{SafeName(Path.GetFileName(file))}";
                await using (var fs = File.OpenRead(file))
                {
                    await store.UploadAsync(objectName, fs, "application/octet-stream");
                }
                // rustfs commits asynchronously — PutObject 200 precedes a statable object;
                // a Pending job would be claimed and fail StatObject within that window
                for (var i = 0; i < 20 && !await store.ExistsAsync(objectName); i++)
                {
                    await Task.Delay(500);
                }
                if (!await store.ExistsAsync(objectName))
                {
                    throw new InvalidOperationException("uploaded object not visible in storage");
                }
                job.ObjectName = objectName;
                job.OriginalFileName = Path.GetFileName(file);
                job.Status = JobStatus.Pending;
                job.Progress = 5;
                await repo.UpdateAsync(job);
                await jobQueue.EnqueueAsync(jobId);
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "URL ingest failed for job {JobId}", jobId);
            try
            {
                using var scope = services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
                var job = await repo.GetByIdAsync(jobId);
                if (job is not null)
                {
                    job.Status = JobStatus.Failed;
                    job.ErrorMessage = $"url ingest: {ex.Message}";
                    job.CompletedAt = DateTime.UtcNow;
                    await repo.UpdateAsync(job);
                    await webhooks.NotifyJobFinishedAsync(job);
                }
            }
            catch (Exception inner)
            {
                logger.LogError(inner, "failed to mark URL-ingest job {JobId} as Failed", jobId);
            }
        }
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

    /// <summary>Reset the job to Pending with new ASR params and re-enqueue.</summary>
    public async Task<JobDto?> RetranscribeAsync(Guid id, string? model, string? language, bool? diarize, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(id, ct);
        if (job is null)
        {
            return null;
        }
        await segments.DeleteByJobAsync(id, ct);
        job.Model = model;
        job.Language = language;
        job.Diarize = diarize ?? true;
        job.Status = JobStatus.Pending;
        job.Progress = 0;
        job.ErrorMessage = null;
        job.Summary = null;
        job.CompletedAt = null;
        job.DurationSeconds = null;
        await jobs.UpdateAsync(job, ct);
        await queue.EnqueueAsync(job.Id, ct);
        return Map(job);
    }

    public async Task<bool?> RetryJobAsync(Guid id, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(id, ct);
        if (job is null)
        {
            return null;
        }
        if (job.Status != JobStatus.Failed)
        {
            return false;
        }
        job.Status = JobStatus.Pending;
        job.Progress = 5;
        job.ErrorMessage = null;
        job.CompletedAt = null;
        await jobs.UpdateAsync(job, ct);
        await queue.EnqueueAsync(id, ct);
        return true;
    }

    /// <summary>S3 object keys: brackets/spaces break GetObject on rustfs — keep [A-Za-z0-9._-].</summary>
    private static string SafeName(string name)
    {
        return string.Concat(name.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
    }

    internal static JobDto Map(TranscriptionJob j) => new(
        j.Id, j.ObjectName, j.OriginalFileName, j.Status.ToString(),
        j.CreatedAt, j.CompletedAt, j.ErrorMessage, j.Model, j.Language,
        j.DurationSeconds, j.Summary, j.Progress, j.SegmentCount > 0 ? j.SegmentCount : j.Segments.Count,
        j.CallbackUrl);
}
