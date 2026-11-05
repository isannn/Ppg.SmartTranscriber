using Microsoft.AspNetCore.Mvc;

using Microsoft.OpenApi.Writers;

using Swashbuckle.AspNetCore.Swagger;

namespace SmartTranscriber.Api.Controllers;

/// <summary>Stable machine-readable OpenAPI document for external consumers.</summary>
[ApiController]
public sealed class OpenApiController(ISwaggerProvider swaggerProvider) : ControllerBase
{
    /// <summary>OpenAPI 3.x JSON — same doc Swagger UI renders, no auth.</summary>
    [HttpGet("/api/v1/openapi.json")]
    public IActionResult Get()
    {
        var doc = swaggerProvider.GetSwagger("v1");
        using var ms = new MemoryStream();
        using (var tw = new StreamWriter(ms))
        {
            var writer = new OpenApiJsonWriter(tw);
            doc.SerializeAsV3(writer);
            tw.Flush();
        }
        ms.Position = 0;
        return File(ms, "application/json");
    }
}
