using System.Collections.Concurrent;

using Microsoft.AspNetCore.SignalR;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;

using SmartTranscriber.Api.Hubs;

namespace SmartTranscriber.Api.Services;

/// <summary>
/// IJobProgressSink over SignalR: keeps the latest update per job for
/// late joiners and broadcasts each report to the job's group.
/// </summary>
public sealed class SignalRJobProgressSink(IHubContext<ProgressHub> hub) : IJobProgressSink
{
    private sealed class State
    {
        public int? Progress;
        public JobStatus? Status;
    }

    private readonly ConcurrentDictionary<Guid, State> _latest = new();

    public void Report(Guid jobId, int? progress = null, JobStatus? status = null)
    {
        var state = _latest.GetOrAdd(jobId, _ => new State());
        if (progress is not null)
        {
            state.Progress = progress;
        }
        if (status is not null)
        {
            state.Status = status;
        }
        var dto = new JobUpdateDto(jobId, state.Progress, state.Status?.ToString());
        _ = hub.Clients.Group(ProgressHub.GroupName(jobId))
            .SendAsync(ProgressHub.EventName, dto);
    }

    public JobUpdateDto? GetSnapshot(Guid jobId)
    {
        return _latest.TryGetValue(jobId, out var s)
            ? new JobUpdateDto(jobId, s.Progress, s.Status?.ToString())
            : null;
    }
}
