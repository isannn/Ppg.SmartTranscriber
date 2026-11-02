namespace SmartTranscriber.Core.Domain;

/// <summary>One extracted key frame of a video job.</summary>
public class MediaFrame
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public int FrameNo { get; set; }
    public double TimeSeconds { get; set; }
    public string ObjectName { get; set; } = "";
}
