using System.Text.Json;

using Microsoft.AspNetCore.Mvc;

using SmartTranscriber.Core.Services;

namespace SmartTranscriber.Api.Controllers;

[ApiController]
[Route("api/v1/export")]
public class ExportController(ExportService export) : ControllerBase
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <summary>Full catalog dump: jobs + segments + media_frames as JSON.</summary>
    [HttpGet]
    public async Task<IActionResult> Dump(CancellationToken ct)
    {
        var dump = await export.DumpAsync(ct);
        var json = JsonSerializer.SerializeToUtf8Bytes(dump, Options);
        return File(json, "application/json",
            $"smarttranscriber-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    }
}
