using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Infrastructure.Transcription;

/// <summary>
/// Retention sweep: deletes Done/Failed jobs older than
/// Retention:MaxAgeDays — job row (segments/frames cascade) plus the
/// S3 media object and every frames/{jobId}/* object.
/// </summary>
public sealed class RetentionWorker(
    IServiceProvider services,
    IOptions<SmartTranscriberOptions> options,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value.Retention;
        if (opts.MaxAgeDays <= 0)
        {
            return;
        }
        logger.LogInformation("Retention sweep every {Min}m, max age {Days}d", opts.SweepMinutes, opts.MaxAgeDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Retention sweep failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(opts.SweepMinutes), stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-options.Value.Retention.MaxAgeDays);
        using var scope = services.CreateScope();
        var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
        var frames = scope.ServiceProvider.GetRequiredService<IFrameRepository>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        var jobs = await jobService.ListJobsAsync(null, int.MaxValue, 0, ct);
        var stale = jobs.Where(j =>
            (j.Status == nameof(JobStatus.Done) || j.Status == nameof(JobStatus.Failed))
            && j.CreatedAt < cutoff).ToList();

        foreach (var j in stale)
        {
            try
            {
                foreach (var f in await frames.GetByJobAsync(j.Id, ct))
                {
                    await storage.DeleteAsync(f.ObjectName, ct);
                }
                await jobService.DeleteJobAsync(j.Id, deleteFile: true, ct);
                logger.LogInformation("Retention deleted job {JobId} ({File})", j.Id, j.OriginalFileName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Retention failed on job {JobId}", j.Id);
            }
        }
    }
}
