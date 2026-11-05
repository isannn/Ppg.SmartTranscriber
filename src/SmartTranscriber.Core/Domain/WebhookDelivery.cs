namespace SmartTranscriber.Core.Domain;

/// <summary>One webhook delivery attempt (audit trail).</summary>
public class WebhookDelivery
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public string Url { get; set; } = "";
    public string Event { get; set; } = "";
    public int? StatusCode { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
