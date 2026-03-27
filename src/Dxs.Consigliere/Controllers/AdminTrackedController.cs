using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Tracking;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Setup;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Controllers;

[Route("api/admin/tracked")]
[Authorize(Policy = AdminAuthDefaults.Policy)]
public class AdminTrackedController(
    INetworkProvider networkProvider,
    IOptions<TransactionFilterConfig> filterConfig) : BaseController
{
    [HttpGet("addresses")]
    [Produces(typeof(AdminTrackedAddressResponse[]))]
    public async Task<IActionResult> GetTrackedAddresses(
        [FromQuery] bool includeTombstoned,
        [FromServices] IAdminTrackingQueryService queryService,
        CancellationToken cancellationToken = default)
        => Ok(await queryService.GetTrackedAddressesAsync(includeTombstoned, cancellationToken));

    [HttpGet("tokens")]
    [Produces(typeof(AdminTrackedTokenResponse[]))]
    public async Task<IActionResult> GetTrackedTokens(
        [FromQuery] bool includeTombstoned,
        [FromServices] IAdminTrackingQueryService queryService,
        CancellationToken cancellationToken = default)
        => Ok(await queryService.GetTrackedTokensAsync(includeTombstoned, cancellationToken));

    [HttpGet("address/{address}")]
    [Produces(typeof(AdminTrackedAddressResponse))]
    public async Task<IActionResult> GetTrackedAddress(
        string address,
        [FromServices] IAdminTrackingQueryService queryService,
        CancellationToken cancellationToken = default)
    {
        if (!Address.TryParse(address, out var parsed))
            return BadRequest($"Unable to parse Address: \"{address}\"");

        var response = await queryService.GetTrackedAddressAsync(parsed.Value, cancellationToken);
        return response is null
            ? NotFound(new { code = "not_tracked", entityId = parsed.Value })
            : Ok(response);
    }

    [HttpGet("token/{tokenId}")]
    [Produces(typeof(AdminTrackedTokenResponse))]
    public async Task<IActionResult> GetTrackedToken(
        string tokenId,
        [FromServices] IAdminTrackingQueryService queryService,
        CancellationToken cancellationToken = default)
    {
        if (!TokenId.TryParse(tokenId, networkProvider.Network, out var parsed))
            return BadRequest($"Unable to parse TokenId: \"{tokenId}\"");

        var response = await queryService.GetTrackedTokenAsync(parsed.Value, cancellationToken);
        return response is null
            ? NotFound(new { code = "not_tracked", entityId = parsed.Value })
            : Ok(response);
    }

    [HttpDelete("address/{address}")]
    [Produces(typeof(AdminTrackedEntityDeleteResponse))]
    public async Task<IActionResult> DeleteTrackedAddress(
        string address,
        [FromServices] ITrackedEntityRegistrationStore registrationStore,
        [FromServices] ITransactionFilter transactionFilter,
        CancellationToken cancellationToken = default)
    {
        if (!Address.TryParse(address, out var parsed))
            return BadRequest($"Unable to parse Address: \"{address}\"");

        if (filterConfig.Value.Addresses.Contains(parsed.Value, StringComparer.Ordinal))
            return Conflict(new { code = "managed_by_config", entityId = parsed.Value });

        if (!await registrationStore.UntrackAddressAsync(parsed.Value, cancellationToken))
            return NotFound(new { code = "not_tracked", entityId = parsed.Value });

        transactionFilter.UnmanageUtxoSetForAddress(parsed);
        return Ok(new AdminTrackedEntityDeleteResponse
        {
            EntityType = Data.Models.Tracking.TrackedEntityType.Address,
            EntityId = parsed.Value,
            Code = "untracked",
            Tombstoned = true,
            TombstonedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    [HttpDelete("token/{tokenId}")]
    [Produces(typeof(AdminTrackedEntityDeleteResponse))]
    public async Task<IActionResult> DeleteTrackedToken(
        string tokenId,
        [FromServices] ITrackedEntityRegistrationStore registrationStore,
        [FromServices] ITransactionFilter transactionFilter,
        CancellationToken cancellationToken = default)
    {
        if (!TokenId.TryParse(tokenId, networkProvider.Network, out var parsed))
            return BadRequest($"Unable to parse TokenId: \"{tokenId}\"");

        if (filterConfig.Value.Tokens.Contains(parsed.Value, StringComparer.Ordinal))
            return Conflict(new { code = "managed_by_config", entityId = parsed.Value });

        if (!await registrationStore.UntrackTokenAsync(parsed.Value, cancellationToken))
            return NotFound(new { code = "not_tracked", entityId = parsed.Value });

        transactionFilter.UnmanageUtxoSetForToken(parsed);
        return Ok(new AdminTrackedEntityDeleteResponse
        {
            EntityType = Data.Models.Tracking.TrackedEntityType.Token,
            EntityId = parsed.Value,
            Code = "untracked",
            Tombstoned = true,
            TombstonedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }
}
