using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace SmartTranscriber.Infrastructure.Media;

/// <summary>
/// ffmpeg segment muxer: splits a media file into fixed-length 16 kHz mono
/// wav chunks (audio only — STT input).
/// </summary>
public sealed class FfmpegAudioChunker(ILogger<FfmpegAudioChunker> logger)
{
    /// <summary>Writes chunk_000.wav… into outDir; returns sorted chunk paths.</summary>
    public async Task<IReadOnlyList<string>> SplitAsync(
        string inputPath, double chunkSeconds, string outDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outDir);
        var pattern = Path.Combine(outDir, "chunk_%03d.wav");
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -i \"{inputPath}\" -vn -ac 1 -ar 16000 -c:a pcm_s16le " +
                        $"-f segment -segment_time {chunkSeconds:0.###} \"{pattern}\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)
                      ?? throw new InvalidOperationException("failed to start ffmpeg");
        await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
        {
            logger.LogWarning("ffmpeg chunking failed ({Code}) for {Input}", p.ExitCode, inputPath);
            return [];
        }
        return Directory.GetFiles(outDir, "chunk_*.wav")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }
}
