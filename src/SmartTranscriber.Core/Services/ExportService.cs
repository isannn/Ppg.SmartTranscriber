using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Core.Services;

/// <summary>
/// Dumps the whole catalog (jobs + segments + media_frames) as one JSON
/// document — for backup / migration.
/// </summary>
public sealed class ExportService(
    IJobRepository jobs,
    ISegmentRepository segments,
    IFrameRepository frames)
{
    public async Task<ExportDump> DumpAsync(CancellationToken ct = default)
    {
        var allJobs = await jobs.ListAsync(null, int.MaxValue, 0, ct);
        var allSegments = new List<TranscriptSegment>();
        var allFrames = new List<MediaFrame>();
        foreach (var job in allJobs)
        {
            allSegments.AddRange(await segments.GetByJobAsync(job.Id, ct));
            allFrames.AddRange(await frames.GetByJobAsync(job.Id, ct));
        }
        return new ExportDump(allJobs, allSegments, allFrames, DateTime.UtcNow);
    }
}

public sealed record ExportDump(
    IReadOnlyList<TranscriptionJob> Jobs,
    IReadOnlyList<TranscriptSegment> Segments,
    IReadOnlyList<MediaFrame> MediaFrames,
    DateTime ExportedAt);
