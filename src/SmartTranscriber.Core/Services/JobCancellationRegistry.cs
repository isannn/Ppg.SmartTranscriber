using System.Collections.Concurrent;

namespace SmartTranscriber.Core.Services;

/// <summary>
/// In-process registry of per-job cancellation tokens. Producers
/// (URL ingest, transcription pipeline) register a CTS for the job they
/// are running; CancelJobAsync cancels it so in-flight work stops at the
/// next await. Single worker host — no cross-process coordination.
/// </summary>
public sealed class JobCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _tokens = new();

    /// <summary>Returns the job's CTS, creating it on first use.</summary>
    public CancellationTokenSource Register(Guid jobId)
    {
        return _tokens.GetOrAdd(jobId, _ => new CancellationTokenSource());
    }

    /// <summary>Cancels the in-flight token when one is registered.</summary>
    public bool TryCancel(Guid jobId)
    {
        if (!_tokens.TryGetValue(jobId, out var cts))
        {
            return false;
        }
        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>Drops and disposes the job's CTS after work finished.</summary>
    public void Remove(Guid jobId)
    {
        if (_tokens.Remove(jobId, out var cts))
        {
            cts.Dispose();
        }
    }
}
