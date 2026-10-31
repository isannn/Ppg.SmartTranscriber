using Microsoft.AspNetCore.Mvc;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Api.Controllers;

[ApiController]
[Route("api/v1/jobs")]
public class JobsController(IJobService jobs, ITranscriptService transcripts) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        JobStatus? parsed = status is null ? null : Enum.Parse<JobStatus>(status, ignoreCase: true);
        var list = await jobs.ListJobsAsync(parsed, Math.Clamp(limit, 1, 500), Math.Max(0, offset), ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id, CancellationToken ct)
    {
        var job = await jobs.GetJobAsync(id, ct);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool deleteFile = true, CancellationToken ct = default)
    {
        return await jobs.DeleteJobAsync(id, deleteFile, ct) ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/segments")]
    public async Task<IActionResult> GetSegments(Guid id, CancellationToken ct)
    {
        var segments = await transcripts.GetSegmentsAsync(id, ct);
        return Ok(segments);
    }

    /// <summary>
    /// Creates a job from a remote media URL (yt-dlp). The media is fetched
    /// in the background; the job sits in Downloading status meanwhile.
    /// </summary>
    [HttpPost("from-url")]
    public async Task<IActionResult> CreateFromUrl([FromBody] UrlRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Url))
        {
            return BadRequest(new { error = "url required" });
        }
        var job = await jobs.CreateUrlJobAsync(request.Url, ct);
        return Accepted(new { job.Id, job.Status });
    }

    public sealed record UrlRequest(string Url);

    /// <summary>
    /// LLM summary. GET returns cached (404 if none), POST regenerates+stores.
    /// </summary>
    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid id, CancellationToken ct)
    {
        var result = await transcripts.GetOrCreateSummaryAsync(id, regenerate: false, ct);
        return result is null
            ? NotFound()
            : Ok(new { summary = result.Value.Summary, cached = result.Value.Cached });
    }

    [HttpPost("{id:guid}/summary")]
    public async Task<IActionResult> CreateSummary(Guid id, CancellationToken ct)
    {
        var result = await transcripts.GetOrCreateSummaryAsync(id, regenerate: true, ct);
        return result is null
            ? NotFound()
            : Ok(new { summary = result.Value.Summary, cached = false });
    }

    /// <summary>
    /// Requeues a Failed job (media object is reused). 409 for non-Failed.
    /// </summary>
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var result = await jobs.RetryJobAsync(id, ct);
        return result switch
        {
            null => NotFound(),
            false => Conflict(new { error = "job is not in Failed status" }),
            _ => Accepted(new { id, status = "Pending" })
        };
    }

    /// <summary>
    /// Bulk speaker rename across all segments of the job.
    /// </summary>
    [HttpPost("{id:guid}/speakers/rename")]
    public async Task<IActionResult> RenameSpeaker(Guid id, [FromBody] RenameRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.From) || string.IsNullOrWhiteSpace(request.To))
        {
            return BadRequest(new { error = "from and to required" });
        }
        var updated = await transcripts.RenameSpeakerAsync(id, request.From, request.To, ct);
        return updated is null ? NotFound() : Ok(new { updated });
    }

    public sealed record RenameRequest(string From, string To);

    /// <summary>
    /// Streams the source media object (HTTP Range enabled for seeking).
    /// </summary>
    [HttpGet("{id:guid}/media")]
    public async Task<IActionResult> GetMedia(Guid id, CancellationToken ct)
    {
        var media = await jobs.DownloadMediaAsync(id, ct);
        if (media is null)
        {
            return NotFound();
        }
        return File(media.Value.Stream, MediaTypes.For(media.Value.ObjectName), enableRangeProcessing: true);
    }

    /// <summary>
    /// Rendered transcript. format=txt (default), json, srt or vtt.
    /// </summary>
    [HttpGet("{id:guid}/transcript")]
    public async Task<IActionResult> GetTranscript(Guid id, [FromQuery] string format = "txt", CancellationToken ct = default)
    {
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var segments = await transcripts.GetSegmentsAsync(id, ct);
            return Ok(segments);
        }

        var fmt = format.ToLowerInvariant() switch
        {
            "srt" => TranscriptFormat.Srt,
            "vtt" => TranscriptFormat.Vtt,
            _ => TranscriptFormat.Txt
        };

        var text = await transcripts.RenderTranscriptAsync(id, fmt, ct);
        if (text is null)
        {
            return NotFound();
        }

        var job = await jobs.GetJobAsync(id, ct);
        var baseName = Path.GetFileNameWithoutExtension(job?.OriginalFileName ?? "transcript");
        return fmt switch
        {
            TranscriptFormat.Srt => File(System.Text.Encoding.UTF8.GetBytes(text), "application/x-subrip", $"{baseName}.srt"),
            TranscriptFormat.Vtt => File(System.Text.Encoding.UTF8.GetBytes(text), "text/vtt", $"{baseName}.vtt"),
            _ => Content(text, "text/plain")
        };
    }
}
