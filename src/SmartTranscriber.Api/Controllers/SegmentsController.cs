using Microsoft.AspNetCore.Mvc;

using SmartTranscriber.Core.Dto;
using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Api.Controllers;

[ApiController]
[Route("api/v1/segments")]
public class SegmentsController(ITranscriptService transcripts) : ControllerBase
{
    [HttpPut("{id:long}/speaker")]
    public async Task<IActionResult> UpdateSpeaker(long id, [FromBody] SpeakerUpdateRequest request, CancellationToken ct)
    {
        return await transcripts.UpdateSpeakerAsync(id, request.Speaker, ct)
            ? NoContent()
            : NotFound();
    }

    /// <summary>Edit a segment's text (generated FTS column re-indexes itself).</summary>
    [HttpPut("{id:long}/text")]
    public async Task<IActionResult> UpdateText(long id, [FromBody] TextUpdateRequest request, CancellationToken ct)
    {
        return await transcripts.UpdateTextAsync(id, request.Text, ct)
            ? NoContent()
            : NotFound();
    }
}
