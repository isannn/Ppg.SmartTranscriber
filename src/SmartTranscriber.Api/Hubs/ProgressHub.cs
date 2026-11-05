using Microsoft.AspNetCore.SignalR;

using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Api.Hubs;

/// <summary>SignalR hub pushing live job progress to subscribed UI clients.</summary>
public sealed class ProgressHub(IJobProgressSink sink) : Hub
{
    public const string EventName = "jobUpdate";

    public static string GroupName(Guid jobId) => $"job-{jobId:N}";

    /// <summary>Subscribe to a job's updates; sends the latest snapshot immediately.</summary>
    public async Task JoinJob(string jobId)
    {
        if (!Guid.TryParse(jobId, out var id))
        {
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(id));
        var snapshot = sink.GetSnapshot(id);
        if (snapshot is not null)
        {
            await Clients.Caller.SendAsync(EventName, snapshot);
        }
    }

    public async Task LeaveJob(string jobId)
    {
        if (Guid.TryParse(jobId, out var id))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(id));
        }
    }
}
