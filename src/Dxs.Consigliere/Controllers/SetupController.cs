using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses.Setup;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

[AllowAnonymous]
[Route("api/setup")]
public class SetupController : BaseController
{
    [HttpGet("status")]
    [Produces(typeof(SetupStatusResponse))]
    public IActionResult GetStatus([FromServices] ISetupWizardService setupWizardService)
        => Ok(setupWizardService.GetStatus());

    [HttpGet("options")]
    [Produces(typeof(SetupOptionsResponse))]
    public async Task<IActionResult> GetOptions(
        [FromServices] ISetupWizardService setupWizardService,
        CancellationToken cancellationToken = default)
        => Ok(await setupWizardService.GetOptionsAsync(cancellationToken));

    [HttpPost("complete")]
    [Produces(typeof(SetupStatusResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(
        [FromBody] SetupCompleteRequest request,
        [FromServices] ISetupWizardService setupWizardService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await setupWizardService.CompleteAsync(request, cancellationToken));
        }
        catch (SetupWizardException ex) when (ex.StatusCode == StatusCodes.Status409Conflict)
        {
            return Conflict(new { code = ex.Code });
        }
        catch (SetupWizardException ex)
        {
            return BadRequest(new { code = ex.Code });
        }
    }
}
