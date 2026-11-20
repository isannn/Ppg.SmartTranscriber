using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Core.Services;
using SmartTranscriber.Infrastructure.Media;

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
    IKeyFrameExtractor keyframes,
    IFrameRepository frames,
    SummarizerChain summarizer,
    IOcr ocr,
    IMediaProbe mediaProbe,
    FfmpegAudioChunker audioChunker,
    IVoiceprintRepository voiceprints,
    IJobProgressSink progressSink,
    JobCancellationRegistry cancellation,
    IOptions<SmartTranscriberOptions> options,
    ILogger<TranscriptionPipeline> logger)
{
    public async Task ProcessAsync(Guid jobId, CancellationToken ct = default)
    {
        var jobCts = cancellation.Register(jobId);
        try
        {
            await ProcessCoreAsync(jobId, jobCts, ct);
        }
        finally
        {
            cancellation.Remove(jobId);
        }
    }

    private async Task ProcessCoreAsync(Guid jobId, CancellationTokenSource jobCts, CancellationToken hostCt)
    {
        var job = await jobs.GetByIdAsync(jobId, hostCt);
        if (job is null)
        {
            logger.LogWarning("Job {JobId} not found, skipping", jobId);
            return;
        }

        if (job.Status == JobStatus.Cancelled)
        {
            // cancelled between the claim and cts registration — nothing to do
            logger.LogInformation("Job {JobId} already cancelled, skipping", jobId);
            return;
        }

        if (job.ObjectName.StartsWith(TranscriptionJob.UrlObjectPrefix, StringComparison.Ordinal))
        {
            // ingest placeholder — media was never uploaded; fail with a
            // clear reason instead of a raw storage ObjectNotFoundException
            job.Status = JobStatus.Failed;
            job.ErrorMessage = "job has no media object — retry to re-download";
            job.CompletedAt = DateTime.UtcNow;
            await jobs.UpdateAsync(job, CancellationToken.None);
            progressSink.Report(job.Id, job.Progress, JobStatus.Failed);
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt, jobCts.Token);
        var ct = linkedCts.Token;

        var tempDir = options.Value.Worker.TempDir;
        // unique per attempt — a retried job must not collide with a file
        // still held by the previous run's ffmpeg/sidecar
        var localPath = Path.Combine(
            string.IsNullOrWhiteSpace(tempDir) ? Path.GetTempPath() : tempDir,
            $"st_{job.Id:N}_{Guid.NewGuid():N}{Path.GetExtension(job.OriginalFileName)}");

        try
        {
            job.Status = JobStatus.Processing;
            job.Progress = 10;
            await jobs.UpdateAsync(job, ct);
            progressSink.Report(job.Id, 10, JobStatus.Processing);

            await using (var input = await storage.DownloadAsync(job.ObjectName, ct))
            await using (var output = File.Create(localPath))
            {
                await input.CopyToAsync(output, ct);
            }

            job.Progress = 25;
            await jobs.UpdateAsync(job, ct);
            progressSink.Report(job.Id, 25);

            // ffprobe container/stream metadata — best-effort, never blocks STT
            MediaMetadata? probedMeta = null;
            try
            {
                probedMeta = await mediaProbe.ProbeAsync(localPath, ct);
                if (probedMeta is not null)
                {
                    job.MediaMeta = JsonSerializer.Serialize(probedMeta, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Media probe failed for job {JobId}", job.Id);
            }

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
                progressSink.Report(job.Id, pct);
                _ = jobs.UpdateAsync(job, CancellationToken.None);
            });

            var stt = options.Value.Stt;
            var transcribeOptions = new TranscribeOptions(
                job.Model ?? stt.DefaultModel,
                job.Language ?? stt.DefaultLanguage,
                job.Diarize,
                job.Hotwords);

            var chunkSeconds = options.Value.Chunking.MaxChunkSeconds;
            var chunked = options.Value.Chunking.Enabled
                && probedMeta?.DurationSeconds > chunkSeconds
                    ? await TranscribeChunkedAsync(localPath, transcribeOptions, chunkSeconds, reporter, ct)
                    : null;

            ITranscriber transcriber;
            TranscriptResult result;
            if (chunked is not null)
            {
                (transcriber, result) = chunked.Value;
            }
            else
            {
                (transcriber, result) = await transcribers.TranscribeAsync(
                    localPath, transcribeOptions, ct, reporter);
            }

            job.Progress = 90;
            await jobs.UpdateAsync(job, ct);
            progressSink.Report(job.Id, 90);

            foreach (var s in result.Segments)
            {
                s.JobId = job.Id;
            }
            await segments.AddRangeAsync(result.Segments, ct);
            await ApplyVoiceprintsAsync(job, result, ct);

            job.Status = JobStatus.Done;
            job.Progress = 100;
            job.CompletedAt = DateTime.UtcNow;
            job.Model = result.Model ?? transcriber.Name;
            job.Language = result.Language;
            job.DurationSeconds = result.DurationSeconds;
            await jobs.UpdateAsync(job, ct);
            progressSink.Report(job.Id, 100, JobStatus.Done);
            logger.LogInformation("Job {JobId} done via {Transcriber}: {Count} segments",
                job.Id, transcriber.Name, result.Segments.Count);
            ExportForIg(job, result.Segments);
            await ExtractFramesAsync(job, localPath, probedMeta, ct);
            await AutoSummarizeAsync(job, result.Segments, ct);
            await AutoTagAsync(job, result.Segments, ct);
            await webhooks.NotifyJobFinishedAsync(job, ct);
        }
        catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
        {
            // user-initiated cancel — terminal, no rethrow
            logger.LogInformation("Job {JobId} cancelled", jobId);
            job.Status = JobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            await jobs.UpdateAsync(job, CancellationToken.None);
            progressSink.Report(job.Id, job.Progress, JobStatus.Cancelled);
            await webhooks.NotifyJobFinishedAsync(job, CancellationToken.None);
        }
        catch (OperationCanceledException) when (hostCt.IsCancellationRequested)
        {
            job.Status = JobStatus.Pending;
            await jobs.UpdateAsync(job, CancellationToken.None);
            progressSink.Report(job.Id, job.Progress, JobStatus.Pending);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed", jobId);
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            await jobs.UpdateAsync(job, CancellationToken.None);
            progressSink.Report(job.Id, job.Progress, JobStatus.Failed);
            await webhooks.NotifyJobFinishedAsync(job, CancellationToken.None);
        }
        finally
        {
            try { File.Delete(localPath); } catch { /* best effort */ }
        }
    }

    /// <summary>Generate a summary right after Done when enabled — best-effort.</summary>
    /// <summary>
    /// Long-file path: ffmpeg splits audio into ~chunkSeconds wav parts,
    /// each transcribed sequentially; segments merged with cumulative
    /// time offsets into one TranscriptResult. Null → caller falls back
    /// to a single pass.
    /// </summary>
    private async Task<(ITranscriber Transcriber, TranscriptResult Result)?> TranscribeChunkedAsync(
        string localPath, TranscribeOptions opts, double chunkSeconds,
        IProgress<double> reporter, CancellationToken ct)
    {
        var tempDir = options.Value.Worker.TempDir;
        var chunkDir = Path.Combine(
            string.IsNullOrWhiteSpace(tempDir) ? Path.GetTempPath() : tempDir,
            $"st_chunks_{Guid.NewGuid():N}");
        IReadOnlyList<string> chunks;
        try
        {
            chunks = await audioChunker.SplitAsync(localPath, chunkSeconds, chunkDir, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audio chunking failed, falling back to single pass");
            return null;
        }
        if (chunks.Count <= 1)
        {
            return null;
        }
        logger.LogInformation("Job split into {Count} chunks of ~{Sec}s", chunks.Count, chunkSeconds);

        var all = new List<TranscriptSegment>();
        ITranscriber? first = null;
        string? model = null, lang = null;
        double? totalDur = 0;
        double offset = 0;
        var n = chunks.Count;
        try
        {
            for (var i = 0; i < n; i++)
            {
                var chunkDur = (await mediaProbe.ProbeAsync(chunks[i], ct))?.DurationSeconds ?? chunkSeconds;
                var sub = new Progress<double>(f => reporter?.Report(Math.Clamp((i + f) / n, 0, 1)));
                var (t, r) = await transcribers.TranscribeAsync(chunks[i], opts, ct, sub);
                first ??= t;
                model ??= r.Model;
                lang ??= r.Language;
                totalDur = (totalDur ?? 0) + (r.DurationSeconds ?? chunkDur);
                foreach (var s in r.Segments)
                {
                    s.StartTime += offset;
                    s.EndTime += offset;
                    all.Add(s);
                }
                offset += chunkDur;
                try { File.Delete(chunks[i]); } catch { /* best effort */ }
            }
        }
        finally
        {
            try { Directory.Delete(chunkDir, true); } catch { /* best effort */ }
        }
        return (first!, new TranscriptResult(all, model, totalDur, lang));
    }

    /// <summary>
    /// Cross-job speaker recognition: match sidecar voiceprints against
    /// stored embeddings, remap segment labels to canonical names,
    /// upsert fresh embeddings. Best-effort — never fails the job.
    /// </summary>
    private async Task ApplyVoiceprintsAsync(TranscriptionJob job, TranscriptResult result, CancellationToken ct)
    {
        var vpOpts = options.Value.Voiceprints;
        if (!vpOpts.Enabled || result.Voiceprints is null || result.Voiceprints.Count == 0)
        {
            return;
        }
        try
        {
            var known = await voiceprints.ListAsync(ct);
            var taken = known.Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var upserts = new List<(string Name, double[] Emb)>();

            foreach (var (label, embF) in result.Voiceprints)
            {
                var emb = embF.Select(x => (double)x).ToArray();
                var match = VoiceprintMatcher.BestMatch(emb, known, vpOpts.MatchThreshold);
                string canonical;
                if (match is not null)
                {
                    canonical = match.Name;
                }
                else
                {
                    canonical = label;
                    for (var n = 2; taken.Contains(canonical); n++)
                    {
                        canonical = $"{label}-{n}";
                    }
                }
                taken.Add(canonical);
                if (!string.Equals(canonical, label, StringComparison.OrdinalIgnoreCase))
                {
                    map[label] = canonical;
                }
                upserts.Add((canonical, emb));
            }

            if (map.Count > 0)
            {
                await segments.RemapSpeakersAsync(job.Id, map, ct);
                foreach (var s in result.Segments)
                {
                    if (s.Speaker is not null && map.TryGetValue(s.Speaker, out var c))
                    {
                        s.Speaker = c;
                    }
                }
                logger.LogInformation("Job {JobId}: voiceprint remap {Map}", job.Id,
                    string.Join(", ", map.Select(kv => $"{kv.Key}→{kv.Value}")));
            }
            foreach (var (name, emb) in upserts)
            {
                await voiceprints.UpsertAsync(name, emb, job.Id, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Voiceprint matching failed for job {JobId}", job.Id);
        }
    }

    private async Task AutoSummarizeAsync(TranscriptionJob job, System.Collections.Generic.IReadOnlyList<TranscriptSegment> segs, CancellationToken ct)
    {
        if (!options.Value.Summary.AutoGenerate)
        {
            return;
        }
        try
        {
            job.Summary = await summarizer.SummarizeAsync(renderer.RenderText(job, segs), null, ct);
            await jobs.UpdateAsync(job, ct);
            logger.LogInformation("Job {JobId}: auto-summary stored ({Chars} chars)", job.Id, job.Summary.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto-summary failed for job {JobId}", job.Id);
        }
    }

    /// <summary>LLM-suggest keywords into job.Tags after Done — best-effort.</summary>
    private async Task AutoTagAsync(TranscriptionJob job, System.Collections.Generic.IReadOnlyList<TranscriptSegment> segs, CancellationToken ct)
    {
        if (!options.Value.Tags.AutoGenerate || !string.IsNullOrWhiteSpace(job.Tags))
        {
            return;
        }
        try
        {
            var prompt = "Suggest 3 to 6 short keyword tags for this transcript, separated by commas, no other text.";
            var raw = await summarizer.SummarizeAsync(renderer.RenderText(job, segs),
                new SmartTranscriber.Core.Dto.SummaryRequest(Prompt: prompt), ct);
            var tags = string.Join(", ", raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(6).Select(t => t.Trim(' ', '.', ';', ':'))
                .Where(t => t.Length is > 1 and <= 40));
            if (!string.IsNullOrWhiteSpace(tags))
            {
                job.Tags = tags;
                await jobs.UpdateAsync(job, ct);
                logger.LogInformation("Job {JobId}: auto-tags stored ({Tags})", job.Id, tags);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto-tags failed for job {JobId}", job.Id);
        }
    }

    private static readonly string[] VideoExtensions = [".mp4", ".webm", ".mkv", ".mov", ".avi"];

    /// <summary>Scene-detect key frames for video jobs → S3 + media_frames.</summary>
    private async Task ExtractFramesAsync(TranscriptionJob job, string localPath, MediaMetadata? probedMeta, CancellationToken ct)
    {
        if (!options.Value.KeyFrames.Enabled
            || probedMeta is { HasVideo: false }
            || !VideoExtensions.Contains(Path.GetExtension(job.OriginalFileName).ToLowerInvariant()))
        {
            return;
        }
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), $"st_kf_{job.Id:N}");
            var extracted = await keyframes.ExtractAsync(localPath, dir, ct);
            var rows = new List<MediaFrame>();
            foreach (var (no, sec, file) in extracted)
            {
                var objectName = $"frames/{job.Id}/{no}.jpg";
                await using var fs = File.OpenRead(file);
                await storage.UploadAsync(objectName, fs, "image/jpeg", ct);
                rows.Add(new MediaFrame { JobId = job.Id, FrameNo = no, TimeSeconds = sec, ObjectName = objectName });
            }
            await frames.InsertBatchAsync(rows, ct);
            logger.LogInformation("Job {JobId}: {Count} key frames extracted", job.Id, rows.Count);

            // OCR each frame into ocr_text — feeds the FTS index (screen-content search)
            if (await ocr.IsAvailableAsync(ct))
            {
                foreach (var (no, _, file) in extracted)
                {
                    var text = await ocr.OcrAsync(file, ct);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        await frames.UpdateOcrAsync(job.Id, no, text, ct);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Key frame extraction failed for job {JobId}", job.Id);
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
