namespace SmartTranscriber.Core.Domain;

/// <summary>Stored speaker embedding for cross-job voice recognition.</summary>
public class Voiceprint
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>L2-normalized embedding as JSON float array.</summary>
    public double[] Embedding { get; set; } = [];
    public Guid? SourceJobId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
