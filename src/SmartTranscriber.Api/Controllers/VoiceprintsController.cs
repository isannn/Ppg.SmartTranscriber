using Microsoft.AspNetCore.Mvc;

using SmartTranscriber.Core.Domain;
using SmartTranscriber.Core.Ports;

namespace SmartTranscriber.Api.Controllers;

/// <summary>Known speaker voiceprints for cross-job recognition.</summary>
[ApiController]
[Route("api/v1/voiceprints")]
public sealed class VoiceprintsController(IVoiceprintRepository voiceprints) : ControllerBase
{
    /// <summary>All stored voiceprints (embedding excluded — identity + provenance only).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<object>>> List(CancellationToken ct)
    {
        var rows = await voiceprints.ListAsync(ct);
        return rows.Select(v => new
        {
            v.Name,
            v.SourceJobId,
            v.CreatedAt,
            dimensions = v.Embedding.Length
        }).ToList();
    }
}
