using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Media;

/// <summary>ffprobe -show_format -show_streams → MediaMetadata.</summary>
public sealed class FfprobeMediaProbe(ILogger<FfprobeMediaProbe> logger) : IMediaProbe
{
    public async Task<MediaMetadata?> ProbeAsync(string path, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -print_format json -show_format -show_streams \"{path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null)
            {
                return null;
            }
            var stdout = await p.StandardOutput.ReadToEndAsync(ct);
            await p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            if (p.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                return null;
            }
            return Parse(stdout);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ffprobe failed for {Path}", path);
            return null;
        }
    }

    private static MediaMetadata? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string? formatName = null;
        double? duration = null;
        long? bitrate = null;
        if (root.TryGetProperty("format", out var fmt))
        {
            formatName = Str(fmt, "format_name");
            duration = Dbl(fmt, "duration");
            bitrate = Long(fmt, "bit_rate");
        }

        string? videoCodec = null, audioCodec = null;
        int? width = null, height = null;
        var hasVideo = false;
        var hasAudio = false;
        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in streams.EnumerateArray())
            {
                var codecType = Str(s, "codec_type");
                if (codecType == "video" && !hasVideo)
                {
                    hasVideo = true;
                    videoCodec = Str(s, "codec_name");
                    width = Int(s, "width");
                    height = Int(s, "height");
                }
                else if (codecType == "audio" && !hasAudio)
                {
                    hasAudio = true;
                    audioCodec = Str(s, "codec_name");
                }
            }
        }
        if (duration is null)
        {
            duration = null; // container duration may be absent; stream-level ignored for simplicity
        }
        return new MediaMetadata(duration, formatName, videoCodec, audioCodec,
            width, height, bitrate, hasVideo, hasAudio);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? Dbl(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) ? d : null;

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static long? Long(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l) ? l : null;
}
