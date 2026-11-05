using SmartTranscriber.Core.Domain;

namespace SmartTranscriber.Core.Ports;

/// <summary>Persistence for webhook delivery attempts.</summary>
public interface IWebhookDeliveryRepository
{
    Task AddRangeAsync(IEnumerable<WebhookDelivery> deliveries, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookDelivery>> ListAsync(Guid? jobId, int limit, CancellationToken ct = default);
}
