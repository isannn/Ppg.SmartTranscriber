using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;

namespace SmartTranscriber.Core.Ports;

/// <summary>Live job progress/status push channel (SignalR in the Api host).</summary>
public interface IJobProgressSink
{
    /// <summary>Report a progress percent and/or status change for a job.</summary>
    void Report(Guid jobId, int? progress = null, JobStatus? status = null);

    /// <summary>Last known update for a job (for late joiners); null if none.</summary>
    JobUpdateDto? GetSnapshot(Guid jobId);
}
