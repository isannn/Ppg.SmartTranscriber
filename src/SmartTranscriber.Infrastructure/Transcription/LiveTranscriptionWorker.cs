using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Core.Services;
using SmartTranscriber.Infrastructure.Live;

namespace SmartTranscriber.Infrastructure.Transcription;

/// <summary>
/// Ticks live sessions: slices new PCM audio (with overlap), transcribes
/// via the sidecar, appends deduplicated segments, pushes progress.
/// </summary>
public sealed class LiveTranscriptionWorker(
    LiveSessionManager sessions,
    IServiceScopeFactory scopeFactory,
    TranscriberChain transcribers,
    IJobProgressSink progressSink,
    IOptions<SmartTranscriberOptions> options,
    ILogger<LiveTranscriptionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var live = options.Value.Live;
        if (!live.Enabled)
        {
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(2, live.TickSeconds)), stoppingToken);
            }
            catch (OperationCanceledException) { return; }

            foreach (var session in sessions.ActiveSessions())
            {
                try
                {
                    await TickAsync(session, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Live tick failed for session {SessionId}", session.Id);
                }
            }
        }
    }

    private async Task TickAsync(LiveSession session, CancellationToken ct)
    {
        var live = options.Value.Live;
        var bps = Math.Max(8000, live.BytesPerSecond);
        var overlapBytes = live.OverlapSeconds * bps;
        var minChunkBytes = live.MinChunkSeconds * bps;
        var finishing = session.Status == "finishing";

        var total = sessions.FileLength(session);
        var start = finishing ? session.CoveredBytes : Math.Max(0, session.CoveredBytes - overlapBytes);
        var end = total;
        if (end - start < (finishing ? 1 : minChunkBytes))
        {
            if (finishing)
            {
                await FinalizeAsync(session, ct);
            }
            return;
        }

        // PCM slice -> wav via ffmpeg
        var raw = sessions.ReadRange(session, start, end);
        var dir = Path.GetDirectoryName(session.TempPath)!;
        var rawPath = Path.Combine(dir, $"st-live-{session.Id:N}.raw");
        var wavPath = Path.Combine(dir, $"st-live-{session.Id:N}.wav");
        await File.WriteAllBytesAsync(rawPath, raw, ct);
        if (!await ToWavAsync(rawPath, wavPath, ct))
        {
            logger.LogWarning("ffmpeg failed for live session {SessionId}", session.Id);
            if (finishing) { await FinalizeAsync(session, ct); }
            return;
        }

        var chunkStartSec = start / (double)bps;
        var coveredRelSec = (session.CoveredBytes - start) / (double)bps;

        var (_, result) = await transcribers.TranscribeAsync(
            wavPath,
            new TranscribeOptions(session.Model, session.Language, session.Diarize && !finishing, session.Hotwords),
            ct);

        // keep only segments starting in the NEW region (past covered frontier)
        var newSegs = result.Segments
            .Where(s => s.StartTime >= coveredRelSec - 0.05)
            .Select(s =>
            {
                s.JobId = session.JobId;
                s.StartTime += chunkStartSec;
                s.EndTime += chunkStartSec;
                return s;
            })
            .ToList();

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var segments = scope.ServiceProvider.GetRequiredService<ISegmentRepository>();
            var jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            if (newSegs.Count > 0)
            {
                await segments.AddRangeAsync(newSegs, ct);
            }
            session.CoveredBytes = end;

            var job = await jobs.GetByIdAsync(session.JobId, ct);
            if (job is not null)
            {
                var coveredSec = session.CoveredBytes / (double)bps;
                var elapsed = Math.Max(1.0, (DateTime.UtcNow - session.StartedAt).TotalSeconds);
                job.Progress = Math.Clamp((int)(coveredSec / elapsed * 100), 0, 95);
                await jobs.UpdateAsync(job, ct);
                progressSink.Report(session.JobId, job.Progress);
            }
        }

        if (finishing)
        {
            await FinalizeAsync(session, ct);
        }
    }

    private async Task FinalizeAsync(LiveSession session, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var webhooks = scope.ServiceProvider.GetRequiredService<IWebhookNotifier>();
        var job = await jobs.GetByIdAsync(session.JobId, CancellationToken.None);
        if (job is not null && job.Status == JobStatus.Processing)
        {
            job.Status = JobStatus.Done;
            job.Progress = 100;
            job.CompletedAt = DateTime.UtcNow;
            await jobs.UpdateAsync(job, CancellationToken.None);
            progressSink.Report(session.JobId, 100, JobStatus.Done);
            await webhooks.NotifyJobFinishedAsync(job, CancellationToken.None);
        }
        sessions.Remove(session.Id);
        logger.LogInformation("Live session {SessionId} finished -> job {JobId}", session.Id, session.JobId);
    }

    private static async Task<bool> ToWavAsync(string rawPath, string wavPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -f s16le -ar 16000 -ac 1 -i \"{rawPath}\" -c:a pcm_s16le \"{wavPath}\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        try
        {
            using var p = Process.Start(psi);
            if (p is null) { return false; }
            await p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            return p.ExitCode == 0 && File.Exists(wavPath);
        }
        catch
        {
            return false;
        }
    }
}
