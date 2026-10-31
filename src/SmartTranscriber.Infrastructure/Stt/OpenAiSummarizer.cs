using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Stt;

/// <summary>
/// OpenAI-compatible summarizer (llama.cpp / Ollama /v1/chat/completions).
/// </summary>
public sealed class OpenAiSummarizer(HttpClient http, IOptions<SmartTranscriberOptions> options) : ISummarizer
{
    public string Name => "openai-compatible";

    private LlmOptions Llm => options.Value.Llm;

    public async Task<string> SummarizeAsync(string transcriptText, SmartTranscriber.Core.Dto.SummaryRequest? request, CancellationToken ct = default)
    {
        var text = transcriptText.Length > Llm.MaxTranscriptChars
            ? transcriptText[..Llm.MaxTranscriptChars]
            : transcriptText;

        var payload = new
        {
            model = string.IsNullOrWhiteSpace(request?.Model) ? Llm.Model : request.Model,
            messages = new object[]
            {
                new { role = "system", content = string.IsNullOrWhiteSpace(request?.Prompt)
                    ? "You summarize transcripts. Reply in the same language as the transcript. Be concise: 5-10 bullet points, then a one-paragraph TL;DR."
                    : request.Prompt! },
                new { role = "user", content = $"Summarize this transcript:\n\n{text}" }
            },
            temperature = 0.3,
            stream = false
        };

        var url = $"{Llm.BaseUrl.TrimEnd('/')}/chat/completions";
        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        if (!string.IsNullOrWhiteSpace(Llm.ApiKey))
        {
            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Llm.ApiKey);
        }
        using var response = await http.SendAsync(message, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"LLM endpoint returned {(int)response.StatusCode}: {body}");
        }

        var parsed = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
        var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
        return string.IsNullOrWhiteSpace(content)
            ? throw new InvalidOperationException("LLM returned an empty summary")
            : content.Trim();
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")] public Message? Message { get; set; }
    }

    private sealed class Message
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}
