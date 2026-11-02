using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;

namespace SmartTranscriber.Core.Ports;

public interface ITranscriber
{
    string Name { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<TranscriptResult> TranscribeAsync(string mediaPath, TranscribeOptions options, CancellationToken ct = default, IProgress<double>? progress = null);
}

public sealed record TranscribeOptions(
    string? Model = null,
    string? Language = null,
    bool Diarize = true);
