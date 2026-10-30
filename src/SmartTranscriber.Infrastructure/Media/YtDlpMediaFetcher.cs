using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Media;

/// <summary>
/// Downloads media URLs via the yt-dlp CLI (system python:
/// `py -3.11 -m yt_dlp`). The final file path is reported by
/// `--print after_move:filepath` on stdout. YouTube needs the SOCKS
/// proxy (host IP is blocked) — `YtDlp:Proxy` or `TG_PROXY` env.
/// </summary>
public sealed class YtDlpMediaFetcher(IOptions<SmartTranscriberOptions> options, ILogger<YtDlpMediaFetcher> logger) : IMediaFetcher
{
    public async Task<string> FetchAsync(string url, string workDir, CancellationToken ct = default)
    {
        var cfg = options.Value.YtDlp;
        var proxy = string.IsNullOrWhiteSpace(cfg.Proxy)
            ? Environment.GetEnvironmentVariable("TG_PROXY")
            : cfg.Proxy;

        var args = $"{cfg.Args} -f bestaudio/best --no-playlist --restrict-filenames " +
                   $"-P \"{workDir}\" --print after_move:filepath " +
                   (string.IsNullOrWhiteSpace(proxy) ? "" : $"--proxy \"{proxy}\" ") +
                   $"-- \"{url}\"";

        var psi = new ProcessStartInfo(cfg.Command, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        logger.LogInformation("yt-dlp fetch: {Url}", url);
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("failed to start yt-dlp");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(cfg.TimeoutSeconds));

        var stdout = await proc.StandardOutput.ReadToEndAsync(cts.Token);
        var stderr = await proc.StandardError.ReadToEndAsync(cts.Token);
        await proc.WaitForExitAsync(cts.Token);

        if (proc.ExitCode != 0)
        {
            var tail = stderr.Split('\n').LastOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
            throw new InvalidOperationException($"yt-dlp exited {proc.ExitCode}: {tail}");
        }

        var file = stdout.Split('\n').LastOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
        if (string.IsNullOrEmpty(file) || !File.Exists(file))
        {
            throw new InvalidOperationException("yt-dlp produced no output file");
        }
        return file;
    }
}
