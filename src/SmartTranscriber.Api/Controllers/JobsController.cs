using Microsoft.AspNetCore.Mvc;

using SmartTranscriber.Core;
using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Dto;
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
        [FromQuery] string? tag,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        JobStatus? parsed = status is null ? null : Enum.Parse<JobStatus>(status, ignoreCase: true);
        var list = await jobs.ListJobsAsync(parsed, tag, Math.Clamp(limit, 1, 500), Math.Max(0, offset), ct);
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
        var job = await jobs.CreateUrlJobAsync(request.Url, request.CallbackUrl, request.Hotwords, request.Priority, ct);
        return Accepted(new { job.Id, job.Status });
    }

    public sealed record UrlRequest(string Url, string? CallbackUrl = null, string? Hotwords = null, int Priority = 0);

    /// <summary>
    /// LLM summary. GET returns cached (404 if none), POST regenerates+stores.
    /// </summary>
    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid id, CancellationToken ct)
    {
        var result = await transcripts.GetOrCreateSummaryAsync(id, regenerate: false, ct: ct);
        return result is null
            ? NotFound()
            : Ok(new { summary = result.Value.Summary, cached = result.Value.Cached });
    }

    [HttpPost("{id:guid}/summary")]
    public async Task<IActionResult> CreateSummary(Guid id, [FromBody] SummaryRequest? request, CancellationToken ct)
    {
        var result = await transcripts.GetOrCreateSummaryAsync(id, request?.Regenerate ?? false, request, ct);
        return result is null
            ? NotFound()
            : Ok(new { summary = result.Value.Summary, cached = result.Value.Cached });
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
        return Ok(new { updated });
    }

    /// <summary>Reset to Pending and re-run transcription with new ASR params.</summary>
    [HttpPost("{id:guid}/retranscribe")]
    public async Task<IActionResult> Retranscribe(Guid id, [FromBody] RetranscribeRequest? request, CancellationToken ct)
    {
        var job = await jobs.RetranscribeAsync(id, request?.Model, request?.Language, request?.Diarize, request?.Hotwords, ct);
        return job is null ? NotFound() : Accepted(job);
    }

    public sealed record RetranscribeRequest(string? Model = null, string? Language = null, bool? Diarize = null, string? Hotwords = null);

    /// <summary>Replace the comma-separated tag list on a job.</summary>
    [HttpPut("{id:guid}/tags")]
    public async Task<IActionResult> UpdateTags(Guid id, [FromBody] TagsRequest request, CancellationToken ct)
    {
        return await jobs.UpdateTagsAsync(id, request?.Tags ?? "", ct) ? Ok(new { updated = true }) : NotFound();
    }

    public sealed record TagsRequest(string Tags);

    /// <summary>Positional transcript diff of two jobs — compare re-transcriptions.</summary>
    [HttpGet("compare")]
    public async Task<IActionResult> Compare([FromQuery] Guid a, [FromQuery] Guid b, CancellationToken ct)
    {
        var result = await jobs.CompareAsync(a, b, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Aggregate stats for dashboards.</summary>
    [HttpGet("/api/v1/stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        return Ok(await jobs.GetStatsAsync(ct));
    }

    /// <summary>Bulk op over job ids: {action:"delete"|"retry", ids:[...]}.</summary>
    [HttpPost("bulk")]
    public async Task<IActionResult> Bulk([FromBody] BulkRequest request, CancellationToken ct)
    {
        if (request?.Ids is not { Count: > 0 } || string.IsNullOrWhiteSpace(request.Action))
        {
            return BadRequest(new { error = "action + ids required" });
        }
        var results = await jobs.BulkAsync(request.Action, request.Ids, ct);
        return Ok(new { results = results.Select(r => new { id = r.Id, ok = r.Ok, detail = r.Detail }) });
    }

    public sealed record BulkRequest(string Action, List<Guid> Ids);

    /// <summary>Merge one speaker label into another (diarization split).</summary>
    [HttpPost("{id:guid}/speakers/merge")]
    public async Task<IActionResult> MergeSpeaker(Guid id, [FromBody] RenameRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.From) || string.IsNullOrWhiteSpace(request.To))
        {
            return BadRequest(new { error = "from/to required" });
        }
        var merged = await transcripts.RenameSpeakerAsync(id, request.From, request.To, ct);
        return Ok(new { merged });
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

    /// <summary>List extracted key frames for a video job.</summary>
    [HttpGet("{id:guid}/frames")]
    public async Task<IActionResult> ListFrames(Guid id, [FromServices] IFrameRepository frames, CancellationToken ct)
    {
        var rows = await frames.GetByJobAsync(id, ct);
        return Ok(rows.Select(f => new
        {
            f.FrameNo,
            f.TimeSeconds,
            url = $"frames/{f.FrameNo}"
        }));
    }

    /// <summary>Stream one key frame (image/jpeg).</summary>
    [HttpGet("{id:guid}/frames/{no:int}")]
    public async Task<IActionResult> GetFrame(Guid id, int no,
        [FromServices] IFrameRepository frames, [FromServices] IStorageService storage, CancellationToken ct)
    {
        var frame = await frames.GetAsync(id, no, ct);
        if (frame is null)
        {
            return NotFound();
        }
        var stream = await storage.DownloadAsync(frame.ObjectName, ct);
        return File(stream, "image/jpeg");
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
