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
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { error = "Query parameter 'q' is required." });
        }
        var hits = await transcripts.SearchAsync(q, Math.Clamp(limit, 1, 200), ct);
        return Ok(hits);
    }
}
