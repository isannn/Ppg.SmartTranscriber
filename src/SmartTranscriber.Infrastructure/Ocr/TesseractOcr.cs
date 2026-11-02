using System.Diagnostics;

using Microsoft.Extensions.Logging;

using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Infrastructure.Ocr;

/// <summary>tesseract.exe subprocess OCR — rus+eng, stdout text mode.</summary>
public sealed class TesseractOcr(ILogger<TesseractOcr> logger) : IOcr
{
    private static string? _binary;

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        _binary ??= await FindBinaryAsync(ct);
        return _binary is not null;
    }

    public async Task<string> OcrAsync(string imagePath, CancellationToken ct = default)
    {
        if (!await IsAvailableAsync(ct))
        {
            return "";
        }
        var psi = new ProcessStartInfo
        {
            FileName = _binary!,
            Arguments = $"\"{imagePath}\" stdout -l rus+eng --psm 6",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)
                      ?? throw new InvalidOperationException("failed to start tesseract");
        var text = await p.StandardOutput.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        return text.Trim();
    }

    private static async Task<string?> FindBinaryAsync(CancellationToken ct)
    {
        foreach (var candidate in new[] { "tesseract", "tesseract.exe" })
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p is not null)
                {
                    await p.WaitForExitAsync(ct);
                    if (p.ExitCode == 0)
                    {
                        return candidate;
                    }
                }
            }
            catch { /* binary not on PATH */ }
        }
        return null;
    }
}
