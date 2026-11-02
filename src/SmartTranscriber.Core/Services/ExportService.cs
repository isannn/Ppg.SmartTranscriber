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
        var allJobs = await jobs.ListAsync(null, null, int.MaxValue, 0, ct);
        var allSegments = new List<TranscriptSegment>();
        var allFrames = new List<MediaFrame>();
        foreach (var job in allJobs)
        {
            allSegments.AddRange(await segments.GetByJobAsync(job.Id, ct));
            allFrames.AddRange(await frames.GetByJobAsync(job.Id, ct));
        }
        return new ExportDump(allJobs, allSegments, allFrames, DateTime.UtcNow);
    }

    /// <summary>
    /// Import a dump produced by DumpAsync — insert jobs whose ids don't
    /// exist yet along with their segments and frames; skip the rest.
    /// </summary>
    public async Task<(int Imported, int Skipped)> ImportDumpAsync(ExportDump dump, CancellationToken ct = default)
    {
        var imported = 0;
        var skipped = 0;
        foreach (var job in dump.Jobs)
        {
            if (await jobs.GetByIdAsync(job.Id, ct) is not null)
            {
                skipped++;
                continue;
            }
            await jobs.AddAsync(job, ct);
            var jobSegments = dump.Segments.Where(s => s.JobId == job.Id).ToList();
            if (jobSegments.Count > 0)
            {
                await segments.AddRangeAsync(jobSegments, ct);
            }
            var jobFrames = dump.MediaFrames.Where(f => f.JobId == job.Id).ToList();
            if (jobFrames.Count > 0)
            {
                await frames.InsertBatchAsync(jobFrames, ct);
            }
            imported++;
        }
        return (imported, skipped);
    }
}

public sealed record ExportDump(
    IReadOnlyList<TranscriptionJob> Jobs,
    IReadOnlyList<TranscriptSegment> Segments,
    IReadOnlyList<MediaFrame> MediaFrames,
    DateTime ExportedAt);
