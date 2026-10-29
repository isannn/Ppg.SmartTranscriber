using System.Threading.Channels;

using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Transcription;

public sealed class JobQueue : IJobQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(jobId, ct);

    public ValueTask<Guid> DequeueAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAsync(ct);
}
