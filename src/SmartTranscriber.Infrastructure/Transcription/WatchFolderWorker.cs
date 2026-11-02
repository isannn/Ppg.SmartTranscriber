using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core;
using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Infrastructure.Transcription;

/// <summary>
/// Watch-folder ingest: polls an inbox directory and creates a
/// transcription job for every new supported media file. Processed
/// files move to DoneDir — that is the idempotency marker.
/// </summary>
public sealed class WatchFolderWorker(
    IServiceProvider services,
    IOptions<SmartTranscriberOptions> options,
    ILogger<WatchFolderWorker> logger) : BackgroundService
{
    private static readonly string[] MediaExtensions =
        [".mp4", ".webm", ".mkv", ".mov", ".avi", ".mp3", ".wav", ".m4a", ".ogg", ".flac", ".opus"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value.WatchFolder;
        if (string.IsNullOrWhiteSpace(opts.Dir))
        {
            return;
        }
        if (!Directory.Exists(opts.Dir))
        {
            logger.LogWarning("WatchFolder dir missing: {Dir}", opts.Dir);
            return;
        }
        logger.LogInformation("WatchFolder watching {Dir} every {Sec}s", opts.Dir, opts.PollSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WatchFolder sweep failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(opts.PollSeconds), stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        var opts = options.Value.WatchFolder;
        var doneDir = string.IsNullOrWhiteSpace(opts.DoneDir)
            ? null
            : Path.Combine(opts.Dir, opts.DoneDir);

        foreach (var file in Directory.EnumerateFiles(opts.Dir))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (!MediaExtensions.Contains(ext) || IsBeingWritten(file))
            {
                continue;
            }

            try
            {
                await using var stream = File.OpenRead(file);
                using var scope = services.CreateScope();
                var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
                var job = await jobService.CreateJobAsync(Path.GetFileName(file), stream,
                    MediaTypes.For(Path.GetFileName(file)), null, null, 0, ct);
                logger.LogInformation("WatchFolder ingested {File} -> job {JobId}", file, job.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WatchFolder failed on {File}", file);
                continue;
            }

            if (doneDir is not null)
            {
                try
                {
                    Directory.CreateDirectory(doneDir);
                    File.Move(file, Path.Combine(doneDir, Path.GetFileName(file)), overwrite: true);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "WatchFolder could not archive {File}", file);
                }
            }
        }
    }

    /// <summary>Skip files that another process is still writing (can't open shared).</summary>
    private static bool IsBeingWritten(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
