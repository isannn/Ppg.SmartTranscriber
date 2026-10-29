using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Stt;

/// <summary>
/// Offline fallback: whisper.cpp binary invoked as a subprocess.
/// Requires SmartTranscriber:Stt:WhisperCppPath and WhisperModelPath.
/// No diarization support.
/// </summary>
public sealed class WhisperCppTranscriber(
    IOptions<SmartTranscriberOptions> options,
    ILogger<WhisperCppTranscriber> logger) : ITranscriber
{
    public string Name => "whisper.cpp";

    private SttOptions Stt => options.Value.Stt;

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        var ok = !string.IsNullOrWhiteSpace(Stt.WhisperCppPath)
                 && File.Exists(Stt.WhisperCppPath)
                 && !string.IsNullOrWhiteSpace(Stt.WhisperModelPath)
                 && File.Exists(Stt.WhisperModelPath);
        return Task.FromResult(ok);
    }

    public async Task<TranscriptResult> TranscribeAsync(string mediaPath, TranscribeOptions transcribeOptions, CancellationToken ct = default)
    {
        var outputBase = Path.Combine(Path.GetTempPath(), $"st_{Guid.NewGuid():N}");
        var jsonPath = outputBase + ".json";

        var args = $"-m \"{Stt.WhisperModelPath}\" -f \"{mediaPath}\" -oj -of \"{outputBase}\"";
        if (!string.IsNullOrWhiteSpace(transcribeOptions.Language))
        {
            args += $" -l {transcribeOptions.Language}";
        }

        var psi = new ProcessStartInfo
        {
            FileName = Stt.WhisperCppPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        logger.LogInformation("Running whisper.cpp: {Path} {Args}", Stt.WhisperCppPath, args);
        using var process = Process.Start(psi)
                          ?? throw new InvalidOperationException("Failed to start whisper.cpp process");
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"whisper.cpp exited with {process.ExitCode}: {stderr}");
        }
        if (!File.Exists(jsonPath))
        {
            throw new InvalidOperationException($"whisper.cpp output not found: {jsonPath}");
        }

        try
        {
            var json = await File.ReadAllTextAsync(jsonPath, ct);
            var doc = JsonSerializer.Deserialize<WhisperOutput>(json)
                      ?? throw new InvalidOperationException("Failed to parse whisper.cpp output");

            var segments = doc.Transcription.Select(t => new TranscriptSegment
            {
                StartTime = t.Offsets.From / 1000.0,
                EndTime = t.Offsets.To / 1000.0,
                Text = t.Text.Trim(),
                Speaker = null
            }).ToList();

            return new TranscriptResult(segments, "whisper.cpp", null, transcribeOptions.Language);
        }
        finally
        {
            try { File.Delete(jsonPath); } catch { /* best effort */ }
        }
    }

    private sealed class WhisperOutput
    {
        [JsonPropertyName("transcription")] public List<WhisperEntry> Transcription { get; set; } = new();
    }

    private sealed class WhisperEntry
    {
        [JsonPropertyName("offsets")] public WhisperOffsets Offsets { get; set; } = new();
        [JsonPropertyName("text")] public string Text { get; set; } = "";
    }

    private sealed class WhisperOffsets
    {
        [JsonPropertyName("from")] public long From { get; set; }
        [JsonPropertyName("to")] public long To { get; set; }
    }
}
