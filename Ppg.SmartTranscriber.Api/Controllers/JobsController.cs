using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ppg.SmartTranscriber.Data;

namespace Ppg.SmartTranscriber.API.Controllers;

[ApiController]
[Route("api/job")]
public class JobController(SmartTranscriberDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCompletedJobs()
    {
        var jobs = await db.TranscriptionJobs
            .Where(j => j.Status == "Done")
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new
            {
                j.Id,
                j.ObjectName,
                j.Status,
                j.CreatedAt
            })
            .ToListAsync();

        return Ok(jobs);
    }

    [HttpGet("{jobId}/segments")]
    public async Task<IActionResult> GetSegments(Guid jobId)
    {
        var segments = await db.TranscriptionSegments
            .Where(s => s.JobId == jobId)
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                s.StartTime,
                s.EndTime,
                s.Text,
                s.Speaker
            })
            .ToListAsync();

        return Ok(segments);
    }

    [HttpPut("segments/{id}/speaker")]
    public async Task<IActionResult> UpdateSpeaker(Guid id, [FromBody] SpeakerUpdateDto dto)
    {
        var segment = await db.TranscriptionSegments.FindAsync(id);
        if (segment == null)
            return NotFound();

        segment.Speaker = dto.Speaker;
        await db.SaveChangesAsync();
        return NoContent();
    }
}