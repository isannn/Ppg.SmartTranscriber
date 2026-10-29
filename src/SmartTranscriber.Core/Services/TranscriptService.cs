using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Core.Services;

public sealed class TranscriptService(
    IJobRepository jobs,
    ISegmentRepository segments,
    ITranscriptRenderer renderer,
    ISummarizer summarizer) : ITranscriptService
{
    public async Task<IReadOnlyList<SegmentDto>> GetSegmentsAsync(Guid jobId, CancellationToken ct = default)
    {
        var list = await segments.GetByJobAsync(jobId, ct);
        return list.Select(s => new SegmentDto(s.Id, s.StartTime, s.EndTime, s.Speaker, s.Text)).ToList();
    }

    public Task<bool> UpdateSpeakerAsync(long segmentId, string? speaker, CancellationToken ct = default)
        => segments.UpdateSpeakerAsync(segmentId, speaker, ct);

    public async Task<string?> RenderTranscriptAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(jobId, ct);
        if (job is null) return null;
        var segs = await segments.GetByJobAsync(jobId, ct);
        return renderer.RenderText(job, segs);
    }

    public async Task<string?> RenderTranscriptAsync(Guid jobId, Ports.TranscriptFormat format, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(jobId, ct);
        if (job is null) return null;
        var segs = await segments.GetByJobAsync(jobId, ct);
        return renderer.Render(job, segs, format);
    }

    public async Task<(string Summary, bool Cached)?> GetOrCreateSummaryAsync(Guid jobId, bool regenerate, CancellationToken ct = default)
    {
        var job = await jobs.GetByIdAsync(jobId, ct);
        if (job is null)
        {
            return null;
        }
        if (!regenerate && !string.IsNullOrWhiteSpace(job.Summary))
        {
            return (job.Summary, true);
        }
        var segs = await segments.GetByJobAsync(jobId, ct);
        var summary = await summarizer.SummarizeAsync(renderer.RenderText(job, segs), ct);
        job.Summary = summary;
        await jobs.UpdateAsync(job, ct);
        return (summary, false);
    }

    public Task<IReadOnlyList<SearchHitDto>> SearchAsync(string query, int limit, CancellationToken ct = default)
        => segments.SearchAsync(query, limit, ct);
}
