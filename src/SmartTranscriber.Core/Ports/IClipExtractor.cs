namespace SmartTranscriber.Core.Ports;

/// <summary>Trims a media file to a time range (ffmpeg).</summary>
public interface IClipExtractor
{
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    /// <summary>Writes outPath and returns true, or false if ffmpeg is missing/failed.</summary>
    Task<bool> ExtractClipAsync(string inputPath, double fromSeconds, double toSeconds, string outPath, CancellationToken ct = default);
}
