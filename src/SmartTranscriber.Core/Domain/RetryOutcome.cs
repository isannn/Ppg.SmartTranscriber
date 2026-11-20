namespace SmartTranscriber.Core.Domain;

/// <summary>Result of a job retry request.</summary>
public enum RetryOutcome
{
    /// <summary>Job id unknown.</summary>
    NotFound,
    /// <summary>Job is not in Failed status — retry refused.</summary>
    NotFailed,
    /// <summary>Requeued straight to transcription (media already stored).</summary>
    Queued,
    /// <summary>URL ingest failed earlier — media is being re-downloaded.</summary>
    Reingesting
}
