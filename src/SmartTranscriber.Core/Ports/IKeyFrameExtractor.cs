namespace SmartTranscriber.Core.Ports;

/// <summary>Extracts key frames from a media file via ffmpeg.</summary>
public interface IKeyFrameExtractor
{
    /// <summary>Returns (frameNo, timeSeconds, localFilePath) sorted by time.</summary>
    Task<IReadOnlyList<(int FrameNo, double TimeSeconds, string FilePath)>> ExtractAsync(
        string mediaPath, string outDir, CancellationToken ct = default);
}
