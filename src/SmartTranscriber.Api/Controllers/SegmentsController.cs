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
}
