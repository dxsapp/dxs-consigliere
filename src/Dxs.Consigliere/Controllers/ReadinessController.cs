using Dxs.Bsv;
using Dxs.Consigliere.Dto.Responses.Readiness;
using Dxs.Consigliere.Services;

using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

[Route("api/readiness")]
public class ReadinessController(INetworkProvider networkProvider) : BaseController
{
    [HttpGet("address/{address}")]
    [Produces(typeof(TrackedEntityReadinessResponse))]
    public async Task<IActionResult> GetAddressReadiness(
        string address,
        [FromServices] ITrackedEntityReadinessService readinessService,
        CancellationToken cancellationToken
    )
    {
        if (!Address.TryParse(address, out var parsed))
            return BadRequest($"Unable to parse Address: \"{address}\"");

        return Ok(await readinessService.GetAddressReadinessAsync(parsed.Value, cancellationToken));
    }

    [HttpGet("token/{tokenId}")]
    [Produces(typeof(TrackedEntityReadinessResponse))]
    public async Task<IActionResult> GetTokenReadiness(
        string tokenId,
        [FromServices] ITrackedEntityReadinessService readinessService,
        CancellationToken cancellationToken
    )
    {
        if (!TokenId.TryParse(tokenId, networkProvider.Network, out var parsed))
            return BadRequest($"Unable to parse TokenId: \"{tokenId}\"");

        return Ok(await readinessService.GetTokenReadinessAsync(parsed.Value, cancellationToken));
    }
}
