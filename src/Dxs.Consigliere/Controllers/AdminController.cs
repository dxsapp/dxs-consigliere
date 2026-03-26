using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.Rpc.Models;
using Dxs.Bsv.Rpc.Services;
using Dxs.Common.Cache;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Tracking;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Dto.Responses.History;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;

using Microsoft.AspNetCore.Mvc;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Controllers;

[Route("api/admin")]
public class AdminController(INetworkProvider networkProvider) : BaseController
{
    [HttpGet("cache/status")]
    [Produces(typeof(ProjectionCacheStatusResponse))]
    public async Task<IActionResult> GetCacheStatus(
        [FromServices] IProjectionReadCacheTelemetry projectionReadCacheTelemetry,
        [FromServices] IProjectionCacheRuntimeStatusReader runtimeStatusReader,
        CancellationToken cancellationToken = default
    )
    {
        var snapshot = projectionReadCacheTelemetry.GetSnapshot();
        var runtime = await runtimeStatusReader.GetSnapshotAsync(cancellationToken);
        return Ok(ProjectionCacheStatusResponseFactory.Build(snapshot, runtime, snapshot.Enabled));
    }

    [HttpPost("manage/address")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ManageAddress(
        [FromBody] WatchAddressRequest request,
        [FromServices] ITrackedEntityRegistrationStore trackedEntityRegistrationStore,
        [FromServices] ITrackedEntityLifecycleOrchestrator trackedEntityLifecycleOrchestrator,
        [FromServices] ITrackedHistoryBackfillScheduler trackedHistoryBackfillScheduler,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] ITransactionFilter transactionFilter
    )
    {
        if (!Address.TryParse(request.Address, out var address))
            return BadRequest($"Unable to parse Address: \"{request.Address}\"");

        try
        {
            var historyMode = NormalizeHistoryMode(request.HistoryPolicy?.Mode);
            await trackedEntityRegistrationStore.RegisterAddressAsync(address.Value, request.Name, historyMode);
            await trackedEntityLifecycleOrchestrator.BeginTrackingAddressAsync(address.Value);
            if (string.Equals(historyMode, HistoryPolicyMode.FullHistory, StringComparison.Ordinal))
            {
                await trackedEntityRegistrationStore.RequestAddressFullHistoryAsync(address.Value);
                await trackedHistoryBackfillScheduler.QueueAddressFullHistoryAsync(address.Value);
            }

            transactionFilter.ManageUtxoSetForAddress(address);
        }
        catch (Exception exception)
        {
            return InternalError(exception.Message);
        }

        return Ok(await readinessService.GetAddressReadinessAsync(address.Value));
    }

    [HttpPost("manage/stas-token")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ManageStasToken(
        [FromBody] WatchStasTokenRequest request,
        [FromServices] ITrackedEntityRegistrationStore trackedEntityRegistrationStore,
        [FromServices] ITrackedEntityLifecycleOrchestrator trackedEntityLifecycleOrchestrator,
        [FromServices] ITrackedHistoryBackfillScheduler trackedHistoryBackfillScheduler,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] ITransactionFilter transactionFilter
    )
    {
        if (!TokenId.TryParse(request.TokenId, networkProvider.Network, out var tokenId))
            return BadRequest($"Unable to parse TokenId: \"{request.TokenId}\"");

        try
        {
            var historyMode = NormalizeHistoryMode(request.HistoryPolicy?.Mode);
            var trustedRoots = NormalizeTrustedRoots(request.TokenHistoryPolicy?.TrustedRoots);
            if (RequiresTrustedRoots(historyMode) && trustedRoots.Length == 0)
                return BadRequest(new { code = "trusted_roots_required", entityId = tokenId.Value });

            await trackedEntityRegistrationStore.RegisterTokenAsync(tokenId.Value, request.Symbol, historyMode, trustedRoots);
            await trackedEntityLifecycleOrchestrator.BeginTrackingTokenAsync(tokenId.Value);
            if (string.Equals(historyMode, HistoryPolicyMode.FullHistory, StringComparison.Ordinal))
            {
                await trackedEntityRegistrationStore.RequestTokenFullHistoryAsync(tokenId.Value, trustedRoots);
                await trackedHistoryBackfillScheduler.QueueTokenFullHistoryAsync(tokenId.Value);
            }

            transactionFilter.ManageUtxoSetForToken(tokenId);
        }
        catch (Exception exception)
        {
            return InternalError(exception.Message);
        }

        return Ok(await readinessService.GetTokenReadinessAsync(tokenId.Value));
    }

    [HttpPost("manage/address/{address}/history/full")]
    [Produces(typeof(TrackedHistoryStatusResponse))]
    public async Task<IActionResult> UpgradeAddressHistory(
        string address,
        [FromServices] ITrackedEntityRegistrationStore trackedEntityRegistrationStore,
        [FromServices] ITrackedEntityLifecycleOrchestrator trackedEntityLifecycleOrchestrator,
        [FromServices] ITrackedHistoryBackfillScheduler trackedHistoryBackfillScheduler,
        [FromServices] ITrackedEntityReadinessService readinessService,
        CancellationToken cancellationToken = default
    )
    {
        if (!Address.TryParse(address, out var parsed))
            return BadRequest($"Unable to parse Address: \"{address}\"");

        if (!await trackedEntityRegistrationStore.RequestAddressFullHistoryAsync(parsed.Value, cancellationToken))
            return Conflict(new { code = "not_tracked", entityId = parsed.Value });

        await trackedEntityLifecycleOrchestrator.BeginTrackingAddressAsync(parsed.Value, cancellationToken);
        await trackedHistoryBackfillScheduler.QueueAddressFullHistoryAsync(parsed.Value, cancellationToken);
        var readiness = await readinessService.GetAddressReadinessAsync(parsed.Value, cancellationToken);
        return Ok(readiness.History);
    }

    [HttpPost("manage/stas-token/{tokenId}/history/full")]
    [Produces(typeof(TrackedHistoryStatusResponse))]
    public async Task<IActionResult> UpgradeTokenHistory(
        string tokenId,
        [FromBody] TokenHistoryPolicyRequest request,
        [FromServices] ITrackedEntityRegistrationStore trackedEntityRegistrationStore,
        [FromServices] ITrackedEntityLifecycleOrchestrator trackedEntityLifecycleOrchestrator,
        [FromServices] ITrackedHistoryBackfillScheduler trackedHistoryBackfillScheduler,
        [FromServices] ITrackedEntityReadinessService readinessService,
        CancellationToken cancellationToken = default
    )
    {
        if (!TokenId.TryParse(tokenId, networkProvider.Network, out var parsed))
            return BadRequest($"Unable to parse TokenId: \"{tokenId}\"");

        var trustedRoots = NormalizeTrustedRoots(request?.TrustedRoots);
        if (trustedRoots.Length == 0)
            return BadRequest(new { code = "trusted_roots_required", entityId = parsed.Value });

        if (!await trackedEntityRegistrationStore.RequestTokenFullHistoryAsync(parsed.Value, trustedRoots, cancellationToken))
            return Conflict(new { code = "not_tracked", entityId = parsed.Value });

        await trackedEntityLifecycleOrchestrator.BeginTrackingTokenAsync(parsed.Value, cancellationToken);
        await trackedHistoryBackfillScheduler.QueueTokenFullHistoryAsync(parsed.Value, cancellationToken);
        var readiness = await readinessService.GetTokenReadinessAsync(parsed.Value, cancellationToken);
        return Ok(readiness.History);
    }

    [HttpPost("manage/address/history/full")]
    [Produces(typeof(BulkHistoryUpgradeResponse))]
    public async Task<IActionResult> UpgradeAddressesHistory(
        [FromBody] BulkAddressHistoryUpgradeRequest request,
        [FromServices] ITrackedEntityRegistrationStore trackedEntityRegistrationStore,
        [FromServices] ITrackedEntityLifecycleOrchestrator trackedEntityLifecycleOrchestrator,
        [FromServices] ITrackedHistoryBackfillScheduler trackedHistoryBackfillScheduler,
        [FromServices] ITrackedEntityReadinessService readinessService,
        CancellationToken cancellationToken = default
    )
    {
        var items = new List<HistoryUpgradeItemResponse>();
        foreach (var raw in request.Addresses.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
        {
            if (!Address.TryParse(raw, out var parsed))
            {
                items.Add(new HistoryUpgradeItemResponse { EntityId = raw, Accepted = false, MessageCode = "invalid_address" });
                continue;
            }

            var accepted = await trackedEntityRegistrationStore.RequestAddressFullHistoryAsync(parsed.Value, cancellationToken);
            if (accepted)
            {
                await trackedEntityLifecycleOrchestrator.BeginTrackingAddressAsync(parsed.Value, cancellationToken);
                await trackedHistoryBackfillScheduler.QueueAddressFullHistoryAsync(parsed.Value, cancellationToken);
            }

            var readiness = await readinessService.GetAddressReadinessAsync(parsed.Value, cancellationToken);
            items.Add(new HistoryUpgradeItemResponse
            {
                EntityId = parsed.Value,
                Accepted = accepted,
                MessageCode = accepted
                    ? (string.Equals(readiness.History?.HistoryReadiness, TrackedEntityHistoryReadiness.FullHistoryLive, StringComparison.Ordinal)
                        ? "already_full_history_live"
                        : "started")
                    : "not_tracked",
                History = readiness.History
            });
        }

        return Ok(new BulkHistoryUpgradeResponse { Items = items.ToArray() });
    }

    [HttpPost("manage/stas-token/history/full")]
    [Produces(typeof(BulkHistoryUpgradeResponse))]
    public async Task<IActionResult> UpgradeTokensHistory(
        [FromBody] BulkTokenHistoryUpgradeRequest request,
        [FromServices] ITrackedEntityRegistrationStore trackedEntityRegistrationStore,
        [FromServices] ITrackedEntityLifecycleOrchestrator trackedEntityLifecycleOrchestrator,
        [FromServices] ITrackedHistoryBackfillScheduler trackedHistoryBackfillScheduler,
        [FromServices] ITrackedEntityReadinessService readinessService,
        CancellationToken cancellationToken = default
    )
    {
        var items = new List<HistoryUpgradeItemResponse>();
        foreach (var item in (request.Items ?? [])
                     .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.TokenId))
                     .GroupBy(x => x.TokenId, StringComparer.Ordinal)
                     .Select(x => x.First()))
        {
            if (!TokenId.TryParse(item.TokenId, networkProvider.Network, out var parsed))
            {
                items.Add(new HistoryUpgradeItemResponse { EntityId = item.TokenId, Accepted = false, MessageCode = "invalid_token_id" });
                continue;
            }

            var trustedRoots = NormalizeTrustedRoots(item.TokenHistoryPolicy?.TrustedRoots);
            if (trustedRoots.Length == 0)
            {
                items.Add(new HistoryUpgradeItemResponse { EntityId = parsed.Value, Accepted = false, MessageCode = "trusted_roots_required" });
                continue;
            }

            var accepted = await trackedEntityRegistrationStore.RequestTokenFullHistoryAsync(parsed.Value, trustedRoots, cancellationToken);
            if (accepted)
            {
                await trackedEntityLifecycleOrchestrator.BeginTrackingTokenAsync(parsed.Value, cancellationToken);
                await trackedHistoryBackfillScheduler.QueueTokenFullHistoryAsync(parsed.Value, cancellationToken);
            }

            var readiness = await readinessService.GetTokenReadinessAsync(parsed.Value, cancellationToken);
            items.Add(new HistoryUpgradeItemResponse
            {
                EntityId = parsed.Value,
                Accepted = accepted,
                MessageCode = accepted
                    ? (string.Equals(readiness.History?.HistoryReadiness, TrackedEntityHistoryReadiness.FullHistoryLive, StringComparison.Ordinal)
                        ? "already_full_history_live"
                        : "started")
                    : "not_tracked",
                History = readiness.History
            });
        }

        return Ok(new BulkHistoryUpgradeResponse { Items = items.ToArray() });
    }

    [HttpGet("blockchain/sync-status")]
    [Produces(typeof(SyncStatusResponse))]
    public async Task<IActionResult> GetSyncState(
        [FromServices] IDocumentStore documentStore,
        [FromServices] IRpcClient rpcClient
    )
    {
        var top = await rpcClient.GetBlockCount().EnsureSuccess();
        var topHash = await rpcClient.GetBlockHash(top).EnsureSuccess();

        using var session = documentStore.GetSession();

        var topKnownBlock = await session
            .Query<BlockProcessContext>()
            .Where(x => x.Height != 0)
            .OrderByDescending(x => x.Height)
            .FirstAsync();
        var isReorg = topKnownBlock != null && top == topKnownBlock.Height && topHash != topKnownBlock.Id;
        var result = new SyncStatusResponse
        {
            Height = top,
            IsSynced = topKnownBlock != null && !isReorg && top == topKnownBlock.Height,
        };

        return Ok(result);
    }

    [HttpPost("manage/stas/backfill")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BackfillStasAttributes(
        [FromServices] IDocumentStore documentStore,
        [FromServices] IMetaTransactionStore transactionStore,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 1000
    )
    {
        if (skip < 0)
            return BadRequest("skip must be >= 0");

        if (take is < 1 or > 5000)
            return BadRequest("take must be in [1..5000]");

        using var session = documentStore.GetNoCacheNoTrackingSession();
        var ids = await session
            .Query<MetaTransaction>()
            .Where(x => x.IsStas)
            .OrderBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .Select(x => x.Id)
            .ToListAsync();

        foreach (var id in ids)
        {
            await transactionStore.UpdateStasAttributes(id);
        }

        return Ok(new
        {
            Skip = skip,
            Take = take,
            Processed = ids.Count,
            HasMore = ids.Count == take
        });
    }

    private static string NormalizeHistoryMode(string mode)
        => string.Equals(mode, HistoryPolicyMode.FullHistory, StringComparison.OrdinalIgnoreCase)
            ? HistoryPolicyMode.FullHistory
            : HistoryPolicyMode.ForwardOnly;

    private static bool RequiresTrustedRoots(string historyMode)
        => string.Equals(historyMode, HistoryPolicyMode.FullHistory, StringComparison.Ordinal);

    private static string[] NormalizeTrustedRoots(IEnumerable<string>? trustedRoots)
        => (trustedRoots ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
