using Microsoft.AspNetCore.Mvc;

namespace PersianAiChat.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Health() => Ok(new { status = "ok", timestamp = DateTime.UtcNow });
}
