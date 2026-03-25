using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Dto.Responses.Readiness;
using Dxs.Consigliere.Services;

using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

[Route("api/address")]
public class AddressController : BaseController
{
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
}
