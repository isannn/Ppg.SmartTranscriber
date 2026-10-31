using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Stt;

/// <summary>
/// Summarizer over tools/llm_bridge — a loopback HTTP front that spawns
/// `devin.exe acp` per request. Primary LLM provider.
/// </summary>
public sealed class AcpSummarizer(HttpClient http, IOptions<SmartTranscriberOptions> options) : IAvailableSummarizer
{
    public string Name => "devin-acp";

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            var response = await http.GetAsync($"{options.Value.Llm.BridgeUrl.TrimEnd('/')}/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> SummarizeAsync(string transcriptText, CancellationToken ct = default)
    {
        var llm = options.Value.Llm;
        var text = transcriptText.Length > llm.MaxTranscriptChars
            ? transcriptText[..llm.MaxTranscriptChars]
            : transcriptText;

        var prompt =
            "You summarize transcripts. Reply in the same language as the transcript. " +
            "Be concise: 5-10 bullet points, then a one-paragraph TL;DR. " +
            "Output ONLY the summary text.\n\n" + text;

        var url = $"{llm.BridgeUrl.TrimEnd('/')}/chat";
        using var response = await http.PostAsJsonAsync(url, new { prompt }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"llm-bridge returned {(int)response.StatusCode}: {body}");
        }

        var parsed = await response.Content.ReadFromJsonAsync<BridgeResponse>(cancellationToken: ct);
        return string.IsNullOrWhiteSpace(parsed?.Text)
            ? throw new InvalidOperationException("llm-bridge returned an empty summary")
            : parsed.Text.Trim();
    }

    private sealed class BridgeResponse
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
    }
}
