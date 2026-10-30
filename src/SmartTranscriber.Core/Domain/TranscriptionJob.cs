namespace SmartTranscriber.Core.Domain;

public class TranscriptionJob
{
    public Guid Id { get; set; }
    public string ObjectName { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Model { get; set; }
    public string? Language { get; set; }
    public double? DurationSeconds { get; set; }
    public string? Summary { get; set; }
    public int Progress { get; set; }
    public int SegmentCount { get; set; }

    public List<TranscriptSegment> Segments { get; set; } = new();
}
