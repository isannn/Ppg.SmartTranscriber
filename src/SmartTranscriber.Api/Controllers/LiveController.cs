using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Options;
using SmartTranscriber.Core.Ports;
using SmartTranscriber.Infrastructure.Live;

namespace SmartTranscriber.Api.Controllers;

/// <summary>Live transcription sessions: stream PCM chunks, get segments in near-real-time.</summary>
[ApiController]
[Route("api/v1/live")]
public sealed class LiveController(
    LiveSessionManager sessions,
    IJobRepository jobs,
    IOptions<SmartTranscriberOptions> options) : ControllerBase
{
    public sealed record CreateLiveRequest(
        string? Language = null, string? Model = null,
        string? Hotwords = null, bool Diarize = true);

    /// <summary>Start a live session. Feed it s16le/16kHz/mono PCM via /chunk.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLiveRequest? req, CancellationToken ct)
    {
        if (!options.Value.Live.Enabled)
        {
            return StatusCode(503, new { error = "live transcription disabled (Live:Enabled)" });
        }
        var sessionId = Guid.NewGuid();
        var job = new TranscriptionJob
        {
            Id = Guid.NewGuid(),
            ObjectName = $"live/{sessionId:N}.pcm",
            OriginalFileName = "live-stream.pcm",
            Status = JobStatus.Processing,
            Language = req?.Language,
            Hotwords = req?.Hotwords,
            Diarize = req?.Diarize ?? true,
            Model = req?.Model
        };
        await jobs.AddAsync(job, ct);
        var session = sessions.Register(new LiveSession
        {
            Id = sessionId,
            JobId = job.Id,
            Language = req?.Language,
            Model = req?.Model,
            Hotwords = req?.Hotwords,
            Diarize = req?.Diarize ?? true
        });
        return Accepted(new { sessionId = session.Id, jobId = session.JobId });
    }

    /// <summary>Append a raw PCM chunk (s16le 16kHz mono).</summary>
    [HttpPost("{id:guid}/chunk")]
    [RequestSizeLimit(64L * 1024 * 1024)]
    public async Task<IActionResult> Chunk(Guid id, CancellationToken ct)
    {
        var buf = new MemoryStream();
        await Request.Body.CopyToAsync(buf, ct);
        var data = buf.GetBuffer().AsMemory(0, (int)buf.Length);
        return sessions.AppendChunk(id, data)
            ? Accepted(new { received = data.Length })
            : NotFound(new { error = "unknown or finished session" });
    }

    /// <summary>Signal end-of-stream: worker finalizes the job (Done + webhook).</summary>
    [HttpPost("{id:guid}/finish")]
    public IActionResult Finish(Guid id)
    {
        var s = sessions.Get(id);
        if (s is null)
        {
            return NotFound(new { error = "unknown session" });
        }
        sessions.Finish(id);
        return Accepted(new { sessionId = id, status = "finishing" });
    }

    /// <summary>Session status snapshot.</summary>
    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id)
    {
        var s = sessions.Get(id);
        return s is null
            ? NotFound(new { error = "unknown session" })
            : Ok(new
            {
                s.Id,
                s.JobId,
                s.Status,
                s.Error,
                coveredBytes = s.CoveredBytes,
                startedAt = s.StartedAt
            });
    }
}
