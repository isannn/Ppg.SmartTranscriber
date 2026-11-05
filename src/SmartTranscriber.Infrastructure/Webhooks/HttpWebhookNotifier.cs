using System.Diagnostics;
using System.Net.Http.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Webhooks;

/// <summary>
/// POSTs a JSON payload to every configured endpoint on job completion
/// and records each attempt in webhook_deliveries.
/// No endpoints configured = silent no-op; per-endpoint failures are
/// logged, persisted and swallowed.
/// </summary>
public sealed class HttpWebhookNotifier(
    HttpClient http,
    IWebhookDeliveryRepository deliveries,
    IOptions<SmartTranscriberOptions> options,
    ILogger<HttpWebhookNotifier> logger) : IWebhookNotifier
{
    public async Task NotifyJobFinishedAsync(TranscriptionJob job, CancellationToken ct = default)
    {
        var endpoints = new List<string>(options.Value.Webhooks.Endpoints);
        if (!string.IsNullOrWhiteSpace(job.CallbackUrl))
        {
            endpoints.Insert(0, job.CallbackUrl);
        }
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

        var attempts = new List<WebhookDelivery>();
        foreach (var endpoint in endpoints)
        {
            var delivery = new WebhookDelivery
            {
                JobId = job.Id,
                Url = endpoint,
                Event = "job.finished",
                CreatedAt = DateTime.UtcNow
            };
            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(options.Value.Webhooks.TimeoutSeconds));
                var response = await http.PostAsJsonAsync(endpoint, payload, cts.Token);
                delivery.StatusCode = (int)response.StatusCode;
                delivery.Success = response.IsSuccessStatusCode;
                logger.LogInformation("Webhook {Endpoint} -> {StatusCode} for job {JobId}",
                    endpoint, delivery.StatusCode, job.Id);
            }
            catch (Exception ex)
            {
                delivery.Error = ex.Message;
                logger.LogWarning(ex, "Webhook {Endpoint} failed for job {JobId}", endpoint, job.Id);
            }
            finally
            {
                sw.Stop();
                delivery.DurationMs = (int)sw.ElapsedMilliseconds;
                attempts.Add(delivery);
            }
        }

        try
        {
            await deliveries.AddRangeAsync(attempts, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist webhook deliveries for job {JobId}", job.Id);
        }
    }
}
