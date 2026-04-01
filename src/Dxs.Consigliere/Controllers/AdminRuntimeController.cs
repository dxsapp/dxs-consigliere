using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Consigliere.Setup;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

[Route("api/admin/runtime")]
[Authorize(Policy = AdminAuthDefaults.Policy)]
public class AdminRuntimeController : BaseController
{
    [HttpGet("sources")]
    [Produces(typeof(AdminRuntimeSourcesResponse))]
    public async Task<IActionResult> GetRuntimeSources(
        [FromServices] IAdminRuntimeSourcePolicyService policyService,
        CancellationToken cancellationToken = default)
        => Ok(await policyService.GetRuntimeSourcesAsync(cancellationToken));

    [HttpPut("sources/realtime-policy")]
    [Produces(typeof(AdminRuntimeSourcesResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateRealtimePolicy(
        [FromBody] AdminRealtimeSourcePolicyUpdateRequest request,
        [FromServices] IAdminRuntimeSourcePolicyService policyService,
        [FromServices] IConsigliereAdminAuthService authService,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { code = "request_required" });

        var authState = await authService.GetStateAsync(User, cancellationToken);
        var updatedBy = User?.Identity?.Name ?? authState.Username ?? "admin";
        var result = await policyService.ApplyRealtimePolicyAsync(
            request.PrimaryRealtimeSource,
            request.BitailsTransport,
            updatedBy,
            cancellationToken);

        if (!result.Success)
            return BadRequest(new { code = result.ErrorCode });

        return Ok(await policyService.GetRuntimeSourcesAsync(cancellationToken));
    }

    [HttpDelete("sources/realtime-policy")]
    [Produces(typeof(AdminRuntimeSourcesResponse))]
    public async Task<IActionResult> ResetRealtimePolicy(
        [FromServices] IAdminRuntimeSourcePolicyService policyService,
        CancellationToken cancellationToken = default)
        => Ok(await policyService.ResetRealtimePolicyAsync(cancellationToken));
}
