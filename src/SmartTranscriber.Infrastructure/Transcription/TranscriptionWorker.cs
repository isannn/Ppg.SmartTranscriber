using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Transcription;

/// <summary>
/// Hosted worker: drains the in-memory job queue; also sweeps the DB for
/// orphaned Pending jobs (e.g. after restart) on a poll interval.
/// </summary>
public sealed class TranscriptionWorker(
    IJobQueue queue,
    IServiceProvider services,
    IOptions<SmartTranscriberOptions> options,
    ILogger<TranscriptionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Worker.Enabled)
        {
            logger.LogInformation("Transcription worker disabled");
            return;
        }

        var sweep = SweepAsync(stoppingToken);
        var drain = DrainAsync(stoppingToken);
        await Task.WhenAll(sweep, drain);
    }

    private async Task DrainAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Guid jobId;
            try
            {
                jobId = await queue.DequeueAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await ProcessJobAsync(jobId, ct);
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.Worker.PollIntervalSeconds));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct);
                while (!ct.IsCancellationRequested)
                {
                    await using var scope = services.CreateAsyncScope();
                    var jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
                    var next = await jobs.ClaimNextPendingAsync(ct);
                    if (next is null)
                    {
                        break;
                    }
                    await ProcessJobAsync(next.Id, ct);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pending-job sweep failed");
            }
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<TranscriptionPipeline>();
        try
        {
            await pipeline.ProcessAsync(jobId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown — job was reset to Pending by the pipeline
        }
    }
}
