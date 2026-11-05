using Microsoft.Extensions.Logging;

using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Core.Services;

/// <summary>On-demand ffmpeg clip of a job's media — S3 → temp → trim → stream.</summary>
public sealed class ClipService(
    IJobRepository jobs,
    IStorageService storage,
    IClipExtractor extractor,
    ILogger<ClipService> logger)
{
    /// <summary>Returns (stream, fileName) for a trimmed clip, or null if job/ffmpeg missing.</summary>
    public async Task<(Stream Stream, string FileName)?> GetClipAsync(Guid id, double from, double to, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(id, ct);
        if (job is null || to <= from)
        {
            return null;
        }
        var inputPath = Path.Combine(Path.GetTempPath(), $"st-clip-{Guid.NewGuid():N}.src");
        var outPath = Path.Combine(Path.GetTempPath(), $"st-clip-{Guid.NewGuid():N}.mp4");
        try
        {
            var media = await storage.DownloadAsync(job.ObjectName, ct);
            await using (media)
            {
                await using var fs = File.Create(inputPath);
                await media.CopyToAsync(fs, ct);
            }
            if (!await extractor.ExtractClipAsync(inputPath, from, to, outPath, ct))
            {
                return null;
            }
            var stream = new FileStream(outPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous | FileOptions.SequentialScan);
            return (stream, $"clip-{id:N}-{from:0.#}-{to:0.#}.mp4");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Clip extraction failed for job {JobId}", id);
            return null;
        }
        finally
        {
            try { File.Delete(inputPath); } catch { /* best effort */ }
        }
    }
}
