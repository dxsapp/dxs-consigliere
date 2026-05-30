using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Tracking;
using Dxs.Consigliere.Dto.Requests;
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

    [HttpPost("addresses")]
    [Produces(typeof(AdminTrackedAddressResponse))]
    public async Task<IActionResult> TrackAddress(
        [FromBody] AdminTrackAddressRequest request,
        [FromServices] ITrackedEntityRegistrationStore registrationStore,
        [FromServices] ITrackedEntityLifecycleOrchestrator lifecycleOrchestrator,
        [FromServices] Services.P2p.RavenWatchlistLoader watchlistLoader,
        [FromServices] IAdminTrackingQueryService queryService,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Address))
            return BadRequest(new { code = "address_required" });

        if (!Address.TryParse(request.Address.Trim(), out var parsed))
            return BadRequest($"Unable to parse Address: \"{request.Address}\"");

        var historyMode = NormalizeHistoryMode(request.HistoryMode);
        if (historyMode is null)
            return BadRequest(new { code = "invalid_history_mode" });

        await registrationStore.RegisterAddressAsync(
            parsed.Value, request.Name?.Trim() ?? string.Empty, historyMode, cancellationToken);
        // Begin tracking so the entity is promoted past "registered" to
        // readable immediately (anchors realtime at the current tip) — the
        // same composition as POST /api/admin/manage/address. Without this
        // the address stays scope_not_ready forever (no background promoter).
        await lifecycleOrchestrator.BeginTrackingAddressAsync(parsed.Value, cancellationToken);
        // Add to the P2P mempool matcher SYNCHRONOUSLY so a tx broadcast
        // right after tracking is matched live — don't rely on the Changes-API
        // hot-reload lag (which leaves a race window where the funding tx is
        // missed and, with no block sync, never recovered).
        watchlistLoader.TrackAddressNow(parsed.Value);

        var response = await queryService.GetTrackedAddressAsync(parsed.Value, cancellationToken);
        return Ok(response);
    }

    [HttpPost("tokens")]
    [Produces(typeof(AdminTrackedTokenResponse))]
    public async Task<IActionResult> TrackToken(
        [FromBody] AdminTrackTokenRequest request,
        [FromServices] ITrackedEntityRegistrationStore registrationStore,
        [FromServices] ITrackedEntityLifecycleOrchestrator lifecycleOrchestrator,
        [FromServices] Services.P2p.RavenWatchlistLoader watchlistLoader,
        [FromServices] IAdminTrackingQueryService queryService,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TokenId))
            return BadRequest(new { code = "token_id_required" });

        if (!TokenId.TryParse(request.TokenId.Trim(), networkProvider.Network, out var parsed))
            return BadRequest($"Unable to parse TokenId: \"{request.TokenId}\"");

        var historyMode = NormalizeHistoryMode(request.HistoryMode);
        if (historyMode is null)
            return BadRequest(new { code = "invalid_history_mode" });

        var trustedRoots = request.TrustedRoots is { Length: > 0 } ? request.TrustedRoots : null;

        await registrationStore.RegisterTokenAsync(
            parsed.Value, request.Symbol?.Trim() ?? string.Empty, historyMode, trustedRoots, cancellationToken);
        // Promote past "registered" to readable immediately (see TrackAddress).
        await lifecycleOrchestrator.BeginTrackingTokenAsync(parsed.Value, cancellationToken);
        // Synchronously add to the P2P matcher (see TrackAddress) so tx are
        // matched live without the Changes-API hot-reload race.
        watchlistLoader.TrackTokenNow(parsed.Value);

        var response = await queryService.GetTrackedTokenAsync(parsed.Value, cancellationToken);
        return Ok(response);
    }

    private static string NormalizeHistoryMode(string requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return TrackedEntityHistoryMode.ForwardOnly;

        var trimmed = requested.Trim();
        return trimmed switch
        {
            TrackedEntityHistoryMode.ForwardOnly => TrackedEntityHistoryMode.ForwardOnly,
            TrackedEntityHistoryMode.FullHistory => TrackedEntityHistoryMode.FullHistory,
            _ => null
        };
    }

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
