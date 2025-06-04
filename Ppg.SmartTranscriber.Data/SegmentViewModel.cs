namespace Ppg.SmartTranscriber.Data;

public class SegmentViewModel
{
    public Guid Id { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Speaker { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    // For UI feedback
    public bool IsValidSpeaker { get; set; }
    public bool IsInvalidSpeaker { get; set; }
}