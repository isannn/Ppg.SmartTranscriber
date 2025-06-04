namespace Ppg.SmartTranscriber.Data;

public class TranscriptSegmentDto
{
    public Guid Id { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Text { get; set; } = "";
    public string? Speaker { get; set; }
}