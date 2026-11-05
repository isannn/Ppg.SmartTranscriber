using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Core.Services;

/// <summary>No-op progress sink — used when no realtime host is attached.</summary>
public sealed class NullJobProgressSink : IJobProgressSink
{
    public void Report(Guid jobId, int? progress = null, JobStatus? status = null)
    {
    }

    public JobUpdateDto? GetSnapshot(Guid jobId) => null;
}
