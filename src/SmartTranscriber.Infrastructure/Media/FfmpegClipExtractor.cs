using System.Diagnostics;

using Microsoft.Extensions.Logging;

using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Media;

/// <summary>ffmpeg -ss/-to trim → mp4 (re-encode for frame-accurate cuts).</summary>
public sealed class FfmpegClipExtractor(ILogger<FfmpegClipExtractor> logger) : IClipExtractor
{
    private static bool? _available;

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        _available ??= await ProbeAsync(ct);
        return _available.Value;
    }

    public async Task<bool> ExtractClipAsync(string inputPath, double fromSeconds, double toSeconds, string outPath, CancellationToken ct = default)
    {
        if (!await IsAvailableAsync(ct) || toSeconds <= fromSeconds)
        {
            return false;
        }
        var duration = toSeconds - fromSeconds;
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -ss {fromSeconds:0.###} -i \"{inputPath}\" -t {duration:0.###} " +
                        "-c:v libx264 -preset veryfast -crf 23 -c:a aac -movflags +faststart \"" + outPath + "\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)
                      ?? throw new InvalidOperationException("failed to start ffmpeg");
        await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0 || !File.Exists(outPath))
        {
            logger.LogWarning("ffmpeg clip failed ({Code}) for {Input} [{From}-{To}]", p.ExitCode, inputPath, fromSeconds, toSeconds);
            return false;
        }
        return true;
    }

    private static async Task<bool> ProbeAsync(CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null)
            {
                return false;
            }
            await p.WaitForExitAsync(ct);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
