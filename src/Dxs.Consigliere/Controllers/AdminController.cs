using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.Rpc.Models;
using Dxs.Bsv.Rpc.Services;
using Dxs.Common.Cache;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Tracking;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
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
        [FromServices] ITransactionFilter transactionFilter
    )
    {
        if (!Address.TryParse(request.Address, out var address))
            return BadRequest($"Unable to parse Address: \"{request.Address}\"");

        try
        {
            await trackedEntityRegistrationStore.RegisterAddressAsync(address.Value, request.Name);
            await trackedEntityLifecycleOrchestrator.BeginTrackingAddressAsync(address.Value);
            transactionFilter.ManageUtxoSetForAddress(address);
        }
        catch (Exception exception)
        {
            return InternalError(exception.Message);
        }

        return Ok();
    }

    [HttpPost("manage/stas-token")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ManageStasToken(
        [FromBody] WatchStasTokenRequest request,
        [FromServices] ITrackedEntityRegistrationStore trackedEntityRegistrationStore,
        [FromServices] ITrackedEntityLifecycleOrchestrator trackedEntityLifecycleOrchestrator,
        [FromServices] ITransactionFilter transactionFilter
    )
    {
        if (!TokenId.TryParse(request.TokenId, networkProvider.Network, out var tokenId))
            return BadRequest($"Unable to parse TokenId: \"{request.TokenId}\"");

        try
        {
            await trackedEntityRegistrationStore.RegisterTokenAsync(tokenId.Value, request.Symbol);
            await trackedEntityLifecycleOrchestrator.BeginTrackingTokenAsync(tokenId.Value);
            transactionFilter.ManageUtxoSetForToken(tokenId);
        }
        catch (Exception exception)
        {
            return InternalError(exception.Message);
        }

        return Ok();
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
}
