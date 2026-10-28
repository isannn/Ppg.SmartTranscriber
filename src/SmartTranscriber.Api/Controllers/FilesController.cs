using Microsoft.AspNetCore.Mvc;

using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Api.Controllers;

[ApiController]
[Route("api/v1/files")]
public class FilesController(IJobService jobs) : ControllerBase
{
    /// <summary>
    /// Upload a media file: stored to MinIO, a Pending transcription job
    /// is created and enqueued.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(4L * 1024 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded." });
        }

        await using var stream = file.OpenReadStream();
        var job = await jobs.CreateJobAsync(file.FileName, stream, file.ContentType, ct);
        return AcceptedAtAction(nameof(JobsController.GetJob), "Jobs", new { id = job.Id }, job);
    }
}
