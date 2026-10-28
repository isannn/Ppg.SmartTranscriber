namespace SmartTranscriber.Core.Domain;

public class TranscriptSegment
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public string? Speaker { get; set; }
    public string Text { get; set; } = "";
}
