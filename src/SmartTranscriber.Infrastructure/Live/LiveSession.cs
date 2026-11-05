namespace SmartTranscriber.Infrastructure.Live;

/// <summary>One live transcription session: growing PCM file + covered offset.</summary>
public sealed class LiveSession
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    /// <summary>"active" | "finishing" | "finished" | "failed"</summary>
    public string Status { get; set; } = "active";
    public string? Error { get; set; }
    public string TempPath { get; set; } = "";
    /// <summary>Bytes already covered by transcription (overlap re-sent on top).</summary>
    public long CoveredBytes { get; set; }
    public string? Language { get; set; }
    public string? Model { get; set; }
    public string? Hotwords { get; set; }
    public bool Diarize { get; set; } = true;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public readonly object Gate = new();
}
