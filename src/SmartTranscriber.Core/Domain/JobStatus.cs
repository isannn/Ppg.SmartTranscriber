namespace SmartTranscriber.Core.Domain;

public enum JobStatus
{
    Pending,
    Downloading,
    Processing,
    Done,
    Failed,
    Cancelled
}
