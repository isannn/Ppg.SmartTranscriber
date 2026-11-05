using Microsoft.AspNetCore.Mvc;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Api.Controllers;

/// <summary>Webhook delivery audit log.</summary>
[ApiController]
[Route("api/v1/webhook-deliveries")]
public sealed class WebhookDeliveriesController(IWebhookDeliveryRepository deliveries) : ControllerBase
{
    /// <summary>Recent delivery attempts, newest first; optional ?jobId= filter.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WebhookDeliveryDto>>> List(
        [FromQuery] Guid? jobId, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var rows = await deliveries.ListAsync(jobId, Math.Clamp(limit, 1, 500), ct);
        return rows.Select(Map).ToList();
    }

    private static WebhookDeliveryDto Map(WebhookDelivery d) => new(
        d.Id, d.JobId, d.Url, d.Event, d.StatusCode, d.Success, d.Error, d.DurationMs, d.CreatedAt);
}
