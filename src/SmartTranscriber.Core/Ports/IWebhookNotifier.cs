using SmartTranscriber.Core.Domain;

namespace SmartTranscriber.Core.Ports;

/// <summary>
/// Fires job-finished webhooks (Done/Failed). Implementations must be
/// best-effort — never throw into the pipeline.
/// </summary>
public interface IWebhookNotifier
{
    Task NotifyJobFinishedAsync(TranscriptionJob job, CancellationToken ct = default);
}
