using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

[Route("api")]
public class ConfigurationController: BaseController
{
    [HttpGet("hc")]
    [AllowAnonymous]
    public IActionResult HealthCheck() => Ok();
}