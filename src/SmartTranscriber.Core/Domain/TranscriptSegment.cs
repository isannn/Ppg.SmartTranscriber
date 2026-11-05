namespace SmartTranscriber.Core.Domain;

public class TranscriptSegment
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public string? Speaker { get; set; }
    public string Text { get; set; } = "";
    /// <summary>ASR confidence 0..1 (faster-whisper only; null for GigaAM/legacy).</summary>
    public double? Confidence { get; set; }
}
