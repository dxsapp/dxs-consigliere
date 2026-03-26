using Dxs.Consigliere.Data.Tokens;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Dto.Responses.Readiness;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;

using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

[Route("api/token")]
public class TokenController : BaseController
{
    [HttpGet("{tokenId}/state")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [Produces(typeof(TokenStateResponse))]
    public async Task<IActionResult> GetState(
        string tokenId,
        [FromServices] INetworkProvider networkProvider,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] TokenProjectionReader tokenProjectionReader,
        [FromServices] TokenProjectionRebuilder tokenProjectionRebuilder,
        CancellationToken cancellationToken
    )
    {
        var normalizedTokenId = tokenId.EnsureValidTokenId(networkProvider.Network).Value;
        var gate = await GetVNextScopeGateAsync(normalizedTokenId, readinessService, cancellationToken);
        if (gate is not null)
            return Conflict(gate);

        await tokenProjectionRebuilder.RebuildAsync(cancellationToken: cancellationToken);

        var state = await tokenProjectionReader.LoadStateAsync(normalizedTokenId, cancellationToken);
        return state is null
            ? NotFound($"Token '{normalizedTokenId}' state was not found.")
            : Ok(TokenStateResponse.From(state));
    }

    [HttpGet("{tokenId}/balances")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [Produces(typeof(BalanceDto[]))]
    public async Task<IActionResult> GetBalances(
        string tokenId,
        [FromServices] INetworkProvider networkProvider,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] TokenProjectionReader tokenProjectionReader,
        [FromServices] TokenProjectionRebuilder tokenProjectionRebuilder,
        CancellationToken cancellationToken
    )
    {
        var normalizedTokenId = tokenId.EnsureValidTokenId(networkProvider.Network).Value;
        var gate = await GetVNextScopeGateAsync(normalizedTokenId, readinessService, cancellationToken);
        if (gate is not null)
            return Conflict(gate);

        await tokenProjectionRebuilder.RebuildAsync(cancellationToken: cancellationToken);

        return Ok((await tokenProjectionReader.LoadBalancesAsync(normalizedTokenId, cancellationToken)).ToArray());
    }

    [HttpGet("{tokenId}/utxos")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [Produces(typeof(GetUtxoSetResponse))]
    public async Task<IActionResult> GetUtxos(
        string tokenId,
        [FromServices] INetworkProvider networkProvider,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] TokenProjectionReader tokenProjectionReader,
        [FromServices] TokenProjectionRebuilder tokenProjectionRebuilder,
        CancellationToken cancellationToken
    )
    {
        var normalizedTokenId = tokenId.EnsureValidTokenId(networkProvider.Network).Value;
        var gate = await GetVNextScopeGateAsync(normalizedTokenId, readinessService, cancellationToken);
        if (gate is not null)
            return Conflict(gate);

        await tokenProjectionRebuilder.RebuildAsync(cancellationToken: cancellationToken);

        var utxos = (await tokenProjectionReader.LoadUtxosAsync(normalizedTokenId, cancellationToken))
            .Select(x => x.ToDto())
            .OrderBy(x => x.Address, StringComparer.Ordinal)
            .ThenBy(x => x.TxId, StringComparer.Ordinal)
            .ThenBy(x => x.Vout)
            .ToArray();

        return Ok(new GetUtxoSetResponse(utxos));
    }

    [HttpGet("{tokenId}/history")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [Produces(typeof(TokenHistoryResponse))]
    public async Task<IActionResult> GetHistory(
        string tokenId,
        [FromQuery] int take,
        [FromServices] INetworkProvider networkProvider,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] TokenProjectionReader tokenProjectionReader,
        [FromServices] TokenProjectionRebuilder tokenProjectionRebuilder,
        CancellationToken cancellationToken
    )
    {
        var normalizedTokenId = tokenId.EnsureValidTokenId(networkProvider.Network).Value;
        var gate = await GetVNextScopeGateAsync(normalizedTokenId, readinessService, cancellationToken);
        if (gate is not null)
            return Conflict(gate);

        await tokenProjectionRebuilder.RebuildAsync(cancellationToken: cancellationToken);

        var history = await tokenProjectionReader.LoadHistoryAsync(
            normalizedTokenId,
            take <= 0 ? 100 : Math.Min(take, 1000),
            cancellationToken
        );

        return Ok(new TokenHistoryResponse
        {
            TokenId = normalizedTokenId,
            History = history.Select(TokenHistoryItemResponse.From).ToArray(),
            TotalCount = history.Count
        });
    }

    private static async Task<TrackedEntityReadinessGateResponse> GetVNextScopeGateAsync(
        string tokenId,
        ITrackedEntityReadinessService readinessService,
        CancellationToken cancellationToken
    )
    {
        var readiness = await readinessService.GetTokenReadinessAsync(tokenId, cancellationToken);
        if (readiness.Tracked && readiness.Readable)
            return null;

        return new TrackedEntityReadinessGateResponse
        {
            Code = readiness.Tracked ? "scope_not_ready" : "not_tracked",
            Entities = [readiness]
        };
    }
}
