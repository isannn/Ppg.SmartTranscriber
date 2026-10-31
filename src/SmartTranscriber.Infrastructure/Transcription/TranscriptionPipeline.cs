using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Infrastructure.Transcription;

/// <summary>
/// Unit of transcription work: download media from storage, run the
/// transcriber chain, persist segments, update job status.
/// </summary>
public sealed class TranscriptionPipeline(
    IJobRepository jobs,
    ISegmentRepository segments,
    IStorageService storage,
    TranscriberChain transcribers,
    IWebhookNotifier webhooks,
    ITranscriptRenderer renderer,
    IOptions<SmartTranscriberOptions> options,
    ILogger<TranscriptionPipeline> logger)
{
    public async Task ProcessAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(jobId, ct);
        if (job is null)
        {
            logger.LogWarning("Job {JobId} not found, skipping", jobId);
            return;
        }

        var tempDir = options.Value.Worker.TempDir;
        var localPath = Path.Combine(
            string.IsNullOrWhiteSpace(tempDir) ? Path.GetTempPath() : tempDir,
            $"st_{job.Id:N}{Path.GetExtension(job.OriginalFileName)}");

        try
        {
            job.Status = JobStatus.Processing;
            job.Progress = 10;
            await jobs.UpdateAsync(job, ct);

            await using (var input = await storage.DownloadAsync(job.ObjectName, ct))
            await using (var output = File.Create(localPath))
            {
                await input.CopyToAsync(output, ct);
            }

            job.Progress = 25;
            await jobs.UpdateAsync(job, ct);

            // live fraction from the sidecar mapped into the 30-90% band,
            // throttled so the DB sees a write at most every ~3s
            var lastWrite = DateTime.MinValue;
            var lastPct = 25;
            var reporter = new Progress<double>(frac =>
            {
                var pct = Math.Clamp(30 + (int)(frac * 60), 30, 89);
                var now = DateTime.UtcNow;
                if (pct <= lastPct || (now - lastWrite).TotalSeconds < 3)
                {
                    return;
                }
                lastPct = pct;
                lastWrite = now;
                job.Progress = pct;
                _ = jobs.UpdateAsync(job, CancellationToken.None);
            });

            var stt = options.Value.Stt;
            var (transcriber, result) = await transcribers.TranscribeAsync(
                localPath,
                new TranscribeOptions(stt.DefaultModel, stt.DefaultLanguage, stt.Diarize),
                ct, reporter);

            job.Progress = 90;
            await jobs.UpdateAsync(job, ct);

            foreach (var s in result.Segments)
            {
                s.JobId = job.Id;
            }
            await segments.AddRangeAsync(result.Segments, ct);

            job.Status = JobStatus.Done;
            job.Progress = 100;
            job.CompletedAt = DateTime.UtcNow;
            job.Model = result.Model ?? transcriber.Name;
            job.Language = result.Language;
            job.DurationSeconds = result.DurationSeconds;
            await jobs.UpdateAsync(job, ct);
            logger.LogInformation("Job {JobId} done via {Transcriber}: {Count} segments",
                job.Id, transcriber.Name, result.Segments.Count);
            ExportForIg(job, result.Segments);
            await webhooks.NotifyJobFinishedAsync(job, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            job.Status = JobStatus.Pending;
            await jobs.UpdateAsync(job, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed", jobId);
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            await jobs.UpdateAsync(job, CancellationToken.None);
            await webhooks.NotifyJobFinishedAsync(job, CancellationToken.None);
        }
        finally
        {
            try { File.Delete(localPath); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Writes the transcript to the InterviewGrocker records folder as
    /// `<OriginalFileName>.txt` — the convention its RecordsScanner walks.
    /// </summary>
    private void ExportForIg(TranscriptionJob job, System.Collections.Generic.IReadOnlyList<TranscriptSegment> segs)
    {
        var dir = options.Value.Consumers.IgExportDir;
        if (string.IsNullOrWhiteSpace(dir))
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(dir);
            var name = $"{Path.GetFileName(job.OriginalFileName)}.txt";
            File.WriteAllText(Path.Combine(dir, name), renderer.RenderText(job, segs));
            logger.LogInformation("Exported transcript for IG: {Name}", name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IG transcript export failed for job {JobId}", job.Id);
        }
    }
}
