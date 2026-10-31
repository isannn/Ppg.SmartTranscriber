using Microsoft.Extensions.Logging;

using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Core.Services;

/// <summary>
/// Strategy chain: tries each registered <see cref="ITranscriber"/> in order
/// and uses the first available one.
/// </summary>
public sealed class TranscriberChain(IEnumerable<ITranscriber> transcribers, ILogger<TranscriberChain> logger)
{
    public async Task<(ITranscriber Transcriber, TranscriptResult Result)> TranscribeAsync(
        string mediaPath, TranscribeOptions options, CancellationToken ct = default,
        IProgress<double>? progress = null)
    {
        var errors = new List<string>();
        foreach (var t in transcribers)
        {
            if (!await t.IsAvailableAsync(ct))
            {
                logger.LogInformation("Transcriber {Name} is not available, skipping", t.Name);
                errors.Add($"{t.Name}: unavailable");
                continue;
            }
            try
            {
                var result = await t.TranscribeAsync(mediaPath, options, ct, progress);
                return (t, result);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Transcriber {Name} failed", t.Name);
                errors.Add($"{t.Name}: {ex.Message}");
            }
        }
        throw new InvalidOperationException("No transcriber succeeded. " + string.Join("; ", errors));
    }
}
