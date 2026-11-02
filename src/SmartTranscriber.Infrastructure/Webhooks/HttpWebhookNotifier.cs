using System.Net.Http.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Webhooks;

/// <summary>
/// POSTs a JSON payload to every configured endpoint on job completion.
/// No endpoints configured = silent no-op; per-endpoint failures are
/// logged and swallowed.
/// </summary>
public sealed class HttpWebhookNotifier(
    HttpClient http,
    IOptions<SmartTranscriberOptions> options,
    ILogger<HttpWebhookNotifier> logger) : IWebhookNotifier
{
    public async Task NotifyJobFinishedAsync(TranscriptionJob job, CancellationToken ct = default)
    {
        var endpoints = options.Value.Webhooks.Endpoints;
        if (endpoints.Count == 0)
        {
            return;
        }

        var payload = new
        {
            @event = "job.finished",
            jobId = job.Id,
            status = job.Status.ToString(),
            job.OriginalFileName,
            job.ErrorMessage,
            job.Model,
            job.DurationSeconds,
            completedAt = job.CompletedAt ?? DateTime.UtcNow
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(options.Value.Webhooks.TimeoutSeconds));
                var response = await http.PostAsJsonAsync(endpoint, payload, cts.Token);
                logger.LogInformation("Webhook {Endpoint} -> {StatusCode} for job {JobId}",
                    endpoint, (int)response.StatusCode, job.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Webhook {Endpoint} failed for job {JobId}", endpoint, job.Id);
            }
        }
    }
}
