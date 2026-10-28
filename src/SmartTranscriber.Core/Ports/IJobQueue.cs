namespace SmartTranscriber.Core.Ports;

public interface IJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default);
    ValueTask<Guid> DequeueAsync(CancellationToken ct = default);
}
