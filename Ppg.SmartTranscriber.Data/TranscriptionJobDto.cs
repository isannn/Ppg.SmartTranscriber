namespace Ppg.SmartTranscriber.Data;

public class TranscriptionJobDto
{
    public Guid Id { get; set; }
    public string ObjectName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}