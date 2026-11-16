
using Microsoft.AspNetCore.Mvc;

namespace TatumWelcome2.Controllers;

[ApiController]
[Route("health")]
[Tags("System Health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Check()
    {
        return Ok(new
        {
            Message = "Tatum Bank API is running."
        });
    }
}
