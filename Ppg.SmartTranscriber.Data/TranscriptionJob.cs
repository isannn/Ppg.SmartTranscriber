namespace Ppg.SmartTranscriber.Data
{
    public class TranscriptionJob
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ObjectName { get; set; } = default!; // filename in MinIO
        public string Status { get; set; } = "Pending"; // Pending, Processing, Done, Failed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? TranscriptText { get; set; }
        public string? ErrorMessage { get; set; }

        public ICollection<TranscriptionSegment> Segments { get; set; } = new List<TranscriptionSegment>();

    }
}
