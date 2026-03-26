using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Dto.Responses.Readiness;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Extensions;

using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

[Route("api/address")]
public class AddressController : BaseController
{
    [HttpGet("{address}/state")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [Produces(typeof(AddressStateResponse))]
    public async Task<IActionResult> GetState(
        string address,
        [FromQuery] string[] tokenIds,
        [FromServices] INetworkProvider networkProvider,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] AddressProjectionReader addressProjectionReader,
        [FromServices] AddressProjectionRebuilder addressProjectionRebuilder,
        CancellationToken cancellationToken
    )
    {
        var gate = await GetVNextScopeGateAsync(address, tokenIds ?? [], networkProvider, readinessService, cancellationToken);
        if (gate is not null)
            return Conflict(gate);

        var normalizedAddress = address.EnsureValidBsvAddress().Value;
        var normalizedTokenIds = NormalizeTokenIds(tokenIds ?? [], networkProvider);

        await addressProjectionRebuilder.RebuildAsync(cancellationToken: cancellationToken);

        var balances = await LoadBalancesAsync(addressProjectionReader, normalizedAddress, normalizedTokenIds, cancellationToken);
        var utxos = await LoadAddressStateUtxosAsync(addressProjectionReader, normalizedAddress, normalizedTokenIds, cancellationToken);

        return Ok(new AddressStateResponse
        {
            Address = normalizedAddress,
            Balances = balances,
            UtxoSet = utxos
        });
    }

    [HttpGet("{address}/balances")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [Produces(typeof(BalanceDto[]))]
    public async Task<IActionResult> GetBalances(
        string address,
        [FromQuery] string[] tokenIds,
        [FromServices] INetworkProvider networkProvider,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] AddressProjectionReader addressProjectionReader,
        [FromServices] AddressProjectionRebuilder addressProjectionRebuilder,
        CancellationToken cancellationToken
    )
    {
        var gate = await GetVNextScopeGateAsync(address, tokenIds ?? [], networkProvider, readinessService, cancellationToken);
        if (gate is not null)
            return Conflict(gate);

        var normalizedAddress = address.EnsureValidBsvAddress().Value;
        var normalizedTokenIds = NormalizeTokenIds(tokenIds ?? [], networkProvider);

        await addressProjectionRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        return Ok(await LoadBalancesAsync(addressProjectionReader, normalizedAddress, normalizedTokenIds, cancellationToken));
    }

    [HttpGet("{address}/utxos")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [Produces(typeof(GetUtxoSetResponse))]
    public async Task<IActionResult> GetUtxoSet(
        string address,
        [FromQuery] string tokenId,
        [FromQuery] long? satoshis,
        [FromServices] INetworkProvider networkProvider,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] IUtxoManager utxoManager,
        CancellationToken cancellationToken
    )
    {
        var gate = await GetVNextScopeGateAsync(
            address,
            string.IsNullOrWhiteSpace(tokenId) ? [] : [tokenId],
            networkProvider,
            readinessService,
            cancellationToken
        );
        if (gate is not null)
            return Conflict(gate);

        var normalizedAddress = address.EnsureValidBsvAddress().Value;
        var normalizedTokenId = string.IsNullOrWhiteSpace(tokenId)
            ? null
            : tokenId.EnsureValidTokenId(networkProvider.Network).Value;

        return Ok(await utxoManager.GetUtxoSet(new GetUtxoSetRequest(normalizedTokenId, normalizedAddress, satoshis)));
    }

    [HttpGet("{address}/history")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [Produces(typeof(AddressHistoryResponse))]
    public async Task<IActionResult> GetHistory(
        string address,
        [FromQuery] string[] tokenIds,
        [FromQuery] bool desc,
        [FromQuery] bool skipZeroBalance,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromServices] INetworkProvider networkProvider,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] IAddressHistoryService addressHistoryService,
        CancellationToken cancellationToken
    )
    {
        var gate = await GetVNextScopeGateAsync(address, tokenIds ?? [], networkProvider, readinessService, cancellationToken);
        if (gate is not null)
            return Conflict(gate);

        var normalizedAddress = address.EnsureValidBsvAddress().Value;
        var normalizedTokenIds = NormalizeTokenIds(tokenIds ?? [], networkProvider);

        return Ok(await addressHistoryService.GetHistory(new GetAddressHistoryRequest(
            normalizedAddress,
            normalizedTokenIds,
            desc,
            skipZeroBalance,
            skip,
            take <= 0 ? 100 : take
        )));
    }

    [HttpPost("balance")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [Produces(typeof(BalanceDto[]))]
    public async Task<IActionResult> GetBalance(
        [FromBody] BalanceRequest request,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] IUtxoManager utxoManager,
        CancellationToken cancellationToken
    )
    {
        var gate = await readinessService.GetBlockingReadinessAsync(request.Addresses, request.TokenIds ?? [], cancellationToken);
        if (gate is not null)
            return Conflict(gate);

        return Ok(await utxoManager.GetBalance(request));
    }

    [HttpPost("batch/utxo-set")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces(typeof(GetUtxoSetResponse))]
    public async Task<IActionResult> GetUtxoSet(
        [FromBody] GetUtxoSetBatchRequest request,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] IUtxoManager utxoManager,
        CancellationToken cancellationToken
    )
    {
        var gate = await readinessService.GetBlockingReadinessAsync(request.Addresses ?? [], request.TokenIds ?? [], cancellationToken);
        if (gate is not null)
            return Conflict(gate);

        return Ok(await utxoManager.GetUtxoSet(request));
    }

    [HttpPost("utxo-set")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces(typeof(GetUtxoSetResponse))]
    public async Task<IActionResult> GetUtxoSet(
        [FromBody] GetUtxoSetRequest request,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] IUtxoManager utxoManager,
        CancellationToken cancellationToken
    )
    {
        var addressSpecified = request.Address is not (null or "");
        var tokenSpecified = request.TokenId is not (null or "");

        if (!addressSpecified && !tokenSpecified)
            return BadRequest("Address or TokenId must be specified, or both");

        var gate = await readinessService.GetBlockingReadinessAsync(
            addressSpecified ? [request.Address] : [],
            tokenSpecified ? [request.TokenId] : [],
            cancellationToken
        );
        if (gate is not null)
            return Conflict(gate);

        return Ok(await utxoManager.GetUtxoSet(request));
    }

    [HttpPost("history")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [Produces(typeof(AddressHistoryResponse))]
    public async Task<IActionResult> GetDetailedHistory(
        [FromBody] GetAddressHistoryRequest request,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] IAddressHistoryService addressHistoryService,
        CancellationToken cancellationToken
    )
    {
        var gate = await readinessService.GetBlockingReadinessAsync([request.Address], request.TokenIds ?? [], cancellationToken);
        if (gate is not null)
            return Conflict(gate);

        return Ok(await addressHistoryService.GetHistory(request));
    }

    private static async Task<TrackedEntityReadinessGateResponse> GetVNextScopeGateAsync(
        string address,
        IEnumerable<string> tokenIds,
        INetworkProvider networkProvider,
        ITrackedEntityReadinessService readinessService,
        CancellationToken cancellationToken
    )
    {
        var normalizedAddress = address.EnsureValidBsvAddress().Value;
        var normalizedTokenIds = NormalizeTokenIds(tokenIds, networkProvider);

        var blocked = new List<TrackedEntityReadinessResponse>();

        var addressReadiness = await readinessService.GetAddressReadinessAsync(normalizedAddress, cancellationToken);
        if (!addressReadiness.Tracked || !addressReadiness.Readable)
            blocked.Add(addressReadiness);

        foreach (var tokenId in normalizedTokenIds)
        {
            var tokenReadiness = await readinessService.GetTokenReadinessAsync(tokenId, cancellationToken);
            if (!tokenReadiness.Tracked || !tokenReadiness.Readable)
                blocked.Add(tokenReadiness);
        }

        if (blocked.Count == 0)
            return null;

        return new TrackedEntityReadinessGateResponse
        {
            Code = blocked.Any(x => !x.Tracked) ? "not_tracked" : "scope_not_ready",
            Entities = blocked.ToArray()
        };
    }

    private static string[] NormalizeTokenIds(IEnumerable<string> tokenIds, INetworkProvider networkProvider)
        => tokenIds?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.EnsureValidTokenId(networkProvider.Network).Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

    private static async Task<BalanceDto[]> LoadBalancesAsync(
        AddressProjectionReader addressProjectionReader,
        string address,
        string[] tokenIds,
        CancellationToken cancellationToken
    )
    {
        var balances = await addressProjectionReader.LoadBsvBalancesAsync([address], cancellationToken);
        if (tokenIds.Length > 0)
        {
            var tokenBalances = await addressProjectionReader.LoadTokenBalancesAsync([address], tokenIds, cancellationToken);
            balances.AddRange(tokenBalances);
        }

        return balances
            .OrderBy(x => x.TokenId ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<UtxoDto[]> LoadAddressStateUtxosAsync(
        AddressProjectionReader addressProjectionReader,
        string address,
        string[] tokenIds,
        CancellationToken cancellationToken
    )
    {
        var utxos = new List<UtxoDto>();
        utxos.AddRange(await addressProjectionReader.LoadP2pkhUtxosAsync([address], 1000, cancellationToken));

        if (tokenIds.Length > 0)
            utxos.AddRange(await addressProjectionReader.LoadTokenUtxosAsync([address], tokenIds, 1000, cancellationToken));

        return utxos
            .OrderBy(x => x.TokenId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(x => x.TxId, StringComparer.Ordinal)
            .ThenBy(x => x.Vout)
            .ToArray();
    }
}
