using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Stt;

/// <summary>
/// STT sidecar client: FastAPI service running GigaAM / faster-whisper
/// with pyannote diarization (tools/stt_svc).
/// </summary>
public sealed class SidecarTranscriber(
    HttpClient http,
    IOptions<SmartTranscriberOptions> options,
    ILogger<SidecarTranscriber> logger) : ITranscriber
{
    public string Name => "stt-sidecar";

    private string BaseUrl => options.Value.Stt.SidecarUrl.TrimEnd('/');

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            var response = await http.GetAsync($"{BaseUrl}/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<TranscriptResult> TranscribeAsync(string mediaPath, TranscribeOptions transcribeOptions, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(mediaPath);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        content.Add(fileContent, "file", Path.GetFileName(mediaPath));

        var query = $"?model={Uri.EscapeDataString(transcribeOptions.Model ?? "")}" +
                    $"&language={Uri.EscapeDataString(transcribeOptions.Language ?? "")}" +
                    $"&diarize={transcribeOptions.Diarize.ToString().ToLowerInvariant()}";

        logger.LogInformation("Sending {File} to stt sidecar at {BaseUrl}", mediaPath, BaseUrl);
        using var response = await http.PostAsync($"{BaseUrl}/transcribe{query}", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"stt sidecar returned {(int)response.StatusCode}: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<SidecarResponse>(cancellationToken: ct)
                      ?? throw new InvalidOperationException("Empty sidecar response");

        var segments = payload.Segments.Select(s => new TranscriptSegment
        {
            StartTime = s.Start,
            EndTime = s.End,
            Text = s.Text.Trim(),
            Speaker = s.Speaker
        }).ToList();

        return new TranscriptResult(segments, payload.Model, payload.Duration, payload.Language);
    }

    private sealed class SidecarResponse
    {
        [JsonPropertyName("segments")] public List<SidecarSegment> Segments { get; set; } = new();
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("language")] public string? Language { get; set; }
        [JsonPropertyName("duration")] public double? Duration { get; set; }
    }

    private sealed class SidecarSegment
    {
        [JsonPropertyName("start")] public double Start { get; set; }
        [JsonPropertyName("end")] public double End { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        [JsonPropertyName("speaker")] public string? Speaker { get; set; }
    }
}
