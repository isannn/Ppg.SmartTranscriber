using Microsoft.AspNetCore.Mvc;

using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Api.Controllers;

[ApiController]
[Route("api/v1/search")]
public class SearchController(ITranscriptService transcripts) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 50,
        [FromQuery] Guid? jobId = null,
        [FromQuery] string? speaker = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? model = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { error = "Query parameter 'q' is required." });
        }
        var hits = await transcripts.SearchAsync(q, Math.Clamp(limit, 1, 200),
            new SmartTranscriber.Core.Dto.SearchFilters(jobId, speaker, from, to, model), ct);
        return Ok(hits);
    }
}
