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
    /// <summary>Import a dump produced by GET /api/v1/export — inserts missing jobs.</summary>
    [HttpPost("/api/v1/import")]
    public async Task<IActionResult> Import([FromBody] ExportDump dump, CancellationToken ct)
    {
        var (imported, skipped) = await export.ImportDumpAsync(dump, ct);
        return Ok(new { imported, skipped });
    }

    [HttpGet]
    public async Task<IActionResult> Dump(CancellationToken ct)
    {
        var dump = await export.DumpAsync(ct);
        var json = JsonSerializer.SerializeToUtf8Bytes(dump, Options);
        return File(json, "application/json",
            $"smarttranscriber-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    }
}
