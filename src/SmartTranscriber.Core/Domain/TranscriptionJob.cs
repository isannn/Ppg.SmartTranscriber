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
    /// <summary>Per-job webhook target — notified on Done/Failed in addition to global endpoints.</summary>
    public string? CallbackUrl { get; set; }
    /// <summary>Run diarization for this job (retranscribe knob).</summary>
    public bool Diarize { get; set; } = true;
    /// <summary>Comma-separated glossary terms biasing ASR (per-job).</summary>
    public string? Hotwords { get; set; }
    /// <summary>Queue priority — higher is claimed first (0 default).</summary>
    public int Priority { get; set; }
    /// <summary>Comma-separated tags for categorization/filtering.</summary>
    public string? Tags { get; set; }
    public int SegmentCount { get; set; }

    public List<TranscriptSegment> Segments { get; set; } = new();
}
