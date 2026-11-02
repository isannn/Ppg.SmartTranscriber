using Microsoft.Extensions.Logging;

using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Core.Services;

/// <summary>
/// Strategy chain: tries each registered <see cref="ISummarizer"/> in
/// order (devin-acp bridge → OpenAI-compatible/OpenRouter fallback) and
/// uses the first that succeeds.
/// </summary>
public sealed class SummarizerChain(IEnumerable<ISummarizer> summarizers, ILogger<SummarizerChain> logger) : ISummarizer
{
    public string Name => "chain";

    public async Task<string> SummarizeAsync(string transcriptText, Dto.SummaryRequest? request, CancellationToken ct = default)
    {
        var errors = new List<string>();
        foreach (var s in summarizers)
        {
            if (s is IAvailableSummarizer probe && !await probe.IsAvailableAsync(ct))
            {
                logger.LogInformation("Summarizer {Name} unavailable, skipping", s.Name);
                errors.Add($"{s.Name}: unavailable");
                continue;
            }
            try
            {
                return await s.SummarizeAsync(transcriptText, request, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Summarizer {Name} failed", s.Name);
                errors.Add($"{s.Name}: {ex.Message}");
            }
        }
        throw new InvalidOperationException("No summarizer succeeded. " + string.Join("; ", errors));
    }
}
