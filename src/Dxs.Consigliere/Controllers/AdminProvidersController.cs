using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Consigliere.Setup;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

[Route("api/admin/providers")]
[Authorize(Policy = AdminAuthDefaults.Policy)]
public class AdminProvidersController : BaseController
{
    [HttpGet]
    [Produces(typeof(AdminProvidersResponse))]
    public async Task<IActionResult> GetProviders(
        [FromServices] IAdminProviderConfigService providerConfigService,
        CancellationToken cancellationToken = default)
        => Ok(await providerConfigService.GetProvidersAsync(cancellationToken));

    [HttpPut("config")]
    [Produces(typeof(AdminProvidersResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProvidersConfig(
        [FromBody] AdminProviderConfigUpdateRequest request,
        [FromServices] IAdminProviderConfigService providerConfigService,
        [FromServices] IConsigliereAdminAuthService authService,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { code = "request_required" });

        var updatedBy = User?.Identity?.Name ?? authService.Username ?? "admin";
        var result = await providerConfigService.ApplyProviderConfigAsync(request, updatedBy, cancellationToken);

        if (!result.Success)
            return BadRequest(new { code = result.ErrorCode });

        return Ok(await providerConfigService.GetProvidersAsync(cancellationToken));
    }

    [HttpDelete("config")]
    [Produces(typeof(AdminProvidersResponse))]
    public async Task<IActionResult> ResetProvidersConfig(
        [FromServices] IAdminProviderConfigService providerConfigService,
        CancellationToken cancellationToken = default)
        => Ok(await providerConfigService.ResetProviderConfigAsync(cancellationToken));
}
