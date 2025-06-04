using NpgsqlTypes;

namespace Ppg.SmartTranscriber.Data;

public class TranscriptionSegment
{
    public int Id { get; set; }

    public Guid JobId { get; set; }
    public TranscriptionJob Job { get; set; } = default!;

    public double StartTime { get; set; }   // in seconds
    public double EndTime { get; set; }     // in seconds

    public string? Speaker { get; set; }    // "Speaker 1", "Speaker 2", or null
    public string Text { get; set; } = default!;

    public NpgsqlTsVector? TextVector { get; set; } // Full-text search vector
}