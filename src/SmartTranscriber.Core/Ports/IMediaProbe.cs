namespace SmartTranscriber.Core.Ports;

/// <summary>Container/stream metadata probed by ffprobe.</summary>
public sealed record MediaMetadata(
    double? DurationSeconds,
    string? FormatName,
    string? VideoCodec,
    string? AudioCodec,
    int? Width,
    int? Height,
    long? BitrateBps,
    bool HasVideo,
    bool HasAudio);

/// <summary>Probes a local media file for container/stream metadata.</summary>
public interface IMediaProbe
{
    Task<MediaMetadata?> ProbeAsync(string path, CancellationToken ct = default);
}
