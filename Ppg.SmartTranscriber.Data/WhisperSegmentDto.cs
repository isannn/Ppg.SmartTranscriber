namespace Ppg.SmartTranscriber.Data;

public class WhisperSegmentDto
{
    public double start { get; set; }
    public double end { get; set; }
    public string text { get; set; } = default!;
    public string? speaker { get; set; }
}