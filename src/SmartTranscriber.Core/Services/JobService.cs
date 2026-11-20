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
    IServiceScopeFactory services,
    ISegmentRepository segments,
    IWebhookNotifier webhooks,
    ILogger<JobService> logger) : IJobService
{
    public async Task<JobDto> CreateJobAsync(string fileName, Stream content, string contentType, string? callbackUrl = null, string? hotwords = null, int priority = 0, CancellationToken ct = default)
    {
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            OriginalFileName = Path.GetFileName(fileName),
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CallbackUrl = callbackUrl,
            Hotwords = hotwords,
            Priority = priority
        };

        // Spool to a temp file so the hash is known before the S3 upload
        // — a content hit skips the upload and the transcription.
        var tmp = Path.Combine(Path.GetTempPath(), $"st-up-{job.Id:N}");
        try
        {
            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true))
            {
                job.ContentHash = await CopyAndHashAsync(content, fs, ct);
            }

            var canonical = await jobs.FindDoneByContentHashAsync(job.ContentHash, job.Id, ct);
            if (canonical is not null)
            {
                await CloneResultsAsync(jobs, segments, webhooks, canonical, job, insert: true, ct);
                logger.LogInformation("Job {JobId} for {FileName} clones done job {CanonicalId} (content hash match)",
                    job.Id, fileName, canonical.Id);
                return Map(job);
            }

            job.ObjectName = $"{Guid.NewGuid():N}_{SafeName(job.OriginalFileName)}";
            await storage.EnsureBucketAsync(ct);
            await using (var fs = File.OpenRead(tmp))
            {
                await storage.UploadAsync(job.ObjectName, fs, contentType, ct);
            }
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* best effort */ }
        }

        await jobs.AddAsync(job, ct);
        await queue.EnqueueAsync(job.Id, ct);

        logger.LogInformation("Created transcription job {JobId} for {FileName}", job.Id, fileName);
        return Map(job);
    }

    /// <summary>
    /// Turns <paramref name="job"/> into a Done clone of
    /// <paramref name="canonical"/>: shared storage object, copied
    /// transcript/meta/summary. The row is persisted as Done (insert
    /// or update), segments are copied after, then the job's own
    /// webhook fires — matching the normal completion path.
    /// </summary>
    private static async Task CloneResultsAsync(
        IJobRepository repo, ISegmentRepository segs, IWebhookNotifier hooks,
        TranscriptionJob canonical, TranscriptionJob job, bool insert, CancellationToken ct)
    {
        job.ObjectName = canonical.ObjectName;
        job.ContentHash = canonical.ContentHash;
        job.DurationSeconds = canonical.DurationSeconds;
        job.MediaMeta = canonical.MediaMeta;
        job.Model = canonical.Model;
        job.Language = canonical.Language;
        job.Summary = canonical.Summary;
        job.Translation = canonical.Translation;
        job.TranslationLang = canonical.TranslationLang;
        job.DuplicateOf = canonical.Id;
        job.Status = JobStatus.Done;
        job.Progress = 100;
        job.CompletedAt = DateTime.UtcNow;
        job.ErrorMessage = null;

        if (insert)
        {
            await repo.AddAsync(job, ct);
        }
        else
        {
            await repo.UpdateAsync(job, ct);
        }

        var copied = (await segs.GetByJobAsync(canonical.Id, ct)).Select(s => new TranscriptSegment
        {
            JobId = job.Id,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            Speaker = s.Speaker,
            Text = s.Text,
            Confidence = s.Confidence
        }).ToList();
        if (copied.Count > 0)
        {
            await segs.AddRangeAsync(copied, ct);
        }
        job.SegmentCount = copied.Count;
        job.Segments = copied;

        await hooks.NotifyJobFinishedAsync(job);
    }

    private static async Task<string> HashFileAsync(string path)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string> CopyAndHashAsync(Stream source, Stream destination, CancellationToken ct)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var buffer = new byte[1 << 20];
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    public async Task<JobDto?> GetJobAsync(Guid id, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(id, ct);
        return job is null ? null : Map(job);
    }

    public async Task<IReadOnlyList<JobDto>> ListJobsAsync(JobStatus? status, string? tag, int limit, int offset, CancellationToken ct = default)
    {
        var list = await jobs.ListAsync(status, tag, limit, offset, ct);
        return list.Select(Map).ToList();
    }

    /// <summary>Replace the comma-separated tag list on a job.</summary>
    public async Task<bool> UpdateTagsAsync(Guid id, string tags, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(id, ct);
        if (job is null)
        {
            return false;
        }
        job.Tags = string.IsNullOrWhiteSpace(tags) ? null : tags.Trim().Trim(',');
        await jobs.UpdateAsync(job, ct);
        return true;
    }

    public async Task<bool> DeleteJobAsync(Guid id, bool deleteFile, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(id, ct);
        if (job is null) return false;

        if (deleteFile)
        {
            // Clones share the canonical job's object — delete the file
            // only when no other job still references it.
            var refs = await jobs.CountByObjectNameAsync(job.ObjectName, id, ct);
            if (refs > 0)
            {
                logger.LogInformation("Keeping shared object {ObjectName}: {Refs} other job(s) reference it", job.ObjectName, refs);
            }
            else
            {
                try { await storage.DeleteAsync(job.ObjectName, ct); }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to delete object {ObjectName}", job.ObjectName); }
            }
        }

        await jobs.DeleteAsync(id, ct);
        return true;
    }

    public async Task<JobDto> CreateUrlJobAsync(string url, string? callbackUrl = null, string? hotwords = null, int priority = 0, CancellationToken ct = default)
    {
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = $"url/{Guid.NewGuid():N}",
            OriginalFileName = url,
            Status = JobStatus.Downloading,
            Progress = 2,
            CreatedAt = DateTime.UtcNow,
            CallbackUrl = callbackUrl,
            Hotwords = hotwords,
            Priority = priority
        };
        try
        {
            job.SourceUrl = UrlNormalizer.Normalize(url);
        }
        catch (UriFormatException)
        {
            job.SourceUrl = null;
        }

        var owner = job.SourceUrl is null
            ? (Job: job, Created: true)
            : await jobs.GetOrAddUrlJobAsync(job, ct);
        if (owner.Created)
        {
            _ = Task.Run(() => IngestUrlAsync(job.Id, url));
            return Map(job);
        }

        var canonical = owner.Job;
        if (canonical.Status == JobStatus.Done)
        {
            // The URL is owned by a finished job — clone its results into
            // a fresh row so this request still gets its own job/webhook.
            job.SourceUrl = null; // canonical keeps the live URL claim
            await CloneResultsAsync(jobs, segments, webhooks, canonical, job, insert: true, ct);
            logger.LogInformation("URL job {JobId} for {Url} clones done job {CanonicalId}", job.Id, url, canonical.Id);
            return Map(job);
        }

        logger.LogInformation("URL {Url} already owned by in-flight job {CanonicalId}", url, canonical.Id);
        return Map(canonical) with { DuplicateOf = canonical.Id };
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
                job.ContentHash = await HashFileAsync(file);
                var canonical = await repo.FindDoneByContentHashAsync(job.ContentHash, jobId);
                if (canonical is not null)
                {
                    job.OriginalFileName = Path.GetFileName(file);
                    var segs2 = scope.ServiceProvider.GetRequiredService<ISegmentRepository>();
                    var hooks = scope.ServiceProvider.GetRequiredService<IWebhookNotifier>();
                    await CloneResultsAsync(repo, segs2, hooks, canonical, job, insert: false, CancellationToken.None);
                    logger.LogInformation("URL ingest job {JobId} clones done job {CanonicalId} (content hash match)", jobId, canonical.Id);
                    return;
                }
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
    public async Task<JobDto?> RetranscribeAsync(Guid id, string? model, string? language, bool? diarize, string? hotwords = null, CancellationToken ct = default)
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
        job.Hotwords = hotwords;
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

    /// <summary>Bulk ops over a list of ids — delete (job+media+frames) or retry failed.</summary>
    public async Task<IReadOnlyList<(Guid Id, bool Ok, string Detail)>> BulkAsync(string action, IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        var results = new List<(Guid, bool, string)>();
        using var scope = services.CreateScope();
        var frameRepo = scope.ServiceProvider.GetService<IFrameRepository>();
        foreach (var id in ids)
        {
            try
            {
                switch (action)
                {
                    case "delete":
                        if (frameRepo is not null)
                        {
                            foreach (var f in await frameRepo.GetByJobAsync(id, ct))
                            {
                                await storage.DeleteAsync(f.ObjectName, ct);
                            }
                        }
                        results.Add((id, await DeleteJobAsync(id, deleteFile: true, ct), "deleted"));
                        break;
                    case "retry":
                        var retried = await RetryJobAsync(id, ct);
                        results.Add((id, retried == true, retried is null ? "not found" : retried == false ? "not Failed" : "queued"));
                        break;
                    default:
                        results.Add((id, false, $"unknown action {action}"));
                        break;
                }
            }
            catch (Exception ex)
            {
                results.Add((id, false, ex.Message));
            }
        }
        return results;
    }

    public Task<JobStatsDto> GetStatsAsync(CancellationToken ct = default) => jobs.GetStatsAsync(ct);

    /// <summary>Positional diff of two jobs' transcripts — same-media retranscribe comparison.</summary>
    public async Task<CompareDto?> CompareAsync(Guid a, Guid b, CancellationToken ct = default)
    {
        var jobA = await jobs.GetByIdAsync(a, ct);
        var jobB = await jobs.GetByIdAsync(b, ct);
        if (jobA is null || jobB is null)
        {
            return null;
        }
        var rows = new List<CompareRow>();
        var count = Math.Max(jobA.Segments.Count, jobB.Segments.Count);
        for (var i = 0; i < count; i++)
        {
            var sa = i < jobA.Segments.Count ? jobA.Segments[i] : null;
            var sb = i < jobB.Segments.Count ? jobB.Segments[i] : null;
            var aText = sa?.Text ?? "";
            var bText = sb?.Text ?? "";
            rows.Add(new CompareRow(i, sa?.StartTime ?? 0, sb?.StartTime ?? 0, aText, bText, aText == bText));
        }
        return new CompareDto(Map(jobA), Map(jobB), rows);
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
        // A resubmission may have re-claimed this URL while the job sat
        // Failed — the unique index only admits one live owner.
        if (job.SourceUrl is not null && await jobs.FindUrlOwnerAsync(job.SourceUrl, id, ct) is not null)
        {
            job.SourceUrl = null;
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

    private static readonly System.Text.Json.JsonSerializerOptions MetaJsonOpts =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    internal static JobDto Map(TranscriptionJob j) => new(
        j.Id, j.ObjectName, j.OriginalFileName, j.Status.ToString(),
        j.CreatedAt, j.CompletedAt, j.ErrorMessage, j.Model, j.Language,
        j.DurationSeconds, j.Summary, j.Progress, j.SegmentCount > 0 ? j.SegmentCount : j.Segments.Count,
        j.CallbackUrl, j.Priority, j.Tags, j.Translation, j.TranslationLang,
        ParseMediaMeta(j.MediaMeta), j.SourceUrl, j.DuplicateOf);

    private static MediaMetaDto? ParseMediaMeta(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<MediaMetaDto>(json, MetaJsonOpts);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
