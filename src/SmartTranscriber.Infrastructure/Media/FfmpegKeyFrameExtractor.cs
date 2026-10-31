using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Media;

/// <summary>
/// ffmpeg scene-detect key frame extractor: writes jpgs and parses
/// pts_time from showinfo stderr to get each frame's media timestamp.
/// Falls back to interval sampling when no scene changes are found.
/// </summary>
public sealed partial class FfmpegKeyFrameExtractor(
    IOptions<SmartTranscriberOptions> options,
    ILogger<FfmpegKeyFrameExtractor> logger) : IKeyFrameExtractor
{
    public async Task<IReadOnlyList<(int FrameNo, double TimeSeconds, string FilePath)>> ExtractAsync(
        string mediaPath, string outDir, CancellationToken ct = default)
    {
        var opts = options.Value.KeyFrames;
        Directory.CreateDirectory(outDir);
        var pattern = Path.Combine(outDir, "kf_%04d.jpg");

        // scene-detect + showinfo -> stderr pts_time per emitted frame
        var args = $"-hide_banner -i \"{mediaPath}\" " +
                   $"-vf \"select='gt(scene,{opts.SceneThreshold})',scale=480:-1,showinfo\" " +
                   $"-vsync vfr -frames:v {opts.MaxFrames} \"{pattern}\"";
        var stderr = await RunFfmpegAsync(args, ct);
        var times = PtsTimeRegex().Matches(stderr)
            .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToList();

        var files = Directory.GetFiles(outDir, "kf_*.jpg").OrderBy(f => f).ToList();
        if (files.Count == 0)
        {
            // fallback: N evenly spaced frames
            await RunFfmpegAsync(
                $"-hide_banner -i \"{mediaPath}\" -vf \"fps=1/{opts.FallbackIntervalSec},scale=480:-1\" " +
                $"-frames:v {opts.MaxFrames} \"{pattern}\"", ct);
            files = Directory.GetFiles(outDir, "kf_*.jpg").OrderBy(f => f).ToList();
            times = Enumerable.Range(1, files.Count).Select(i => (double)i * opts.FallbackIntervalSec).ToList();
        }

        var result = new List<(int, double, string)>();
        for (var i = 0; i < files.Count; i++)
        {
            result.Add((i + 1, i < times.Count ? times[i] : 0, files[i]));
        }
        return result;
    }

    private async Task<string> RunFfmpegAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = args,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)
                      ?? throw new InvalidOperationException("failed to start ffmpeg");
        var stderr = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
        {
            logger.LogWarning("ffmpeg exited {Code}: {Tail}", p.ExitCode, stderr[^Math.Min(300, stderr.Length)..]);
        }
        return stderr;
    }

    [GeneratedRegex(@"pts_time:([0-9.]+)")]
    private static partial Regex PtsTimeRegex();
}
