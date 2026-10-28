using Microsoft.AspNetCore.Mvc;

namespace SmartTranscriber.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "ok", service = "smarttranscriber" });
    }
}
