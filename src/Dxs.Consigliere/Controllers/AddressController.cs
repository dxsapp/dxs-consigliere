using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Services;

using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

[Route("api/address")]
public class AddressController : BaseController
{
    [HttpPost("balance")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces(typeof(BalanceDto[]))]
    public async Task<IActionResult> GetBalance(
        [FromBody] BalanceRequest request,
        [FromServices] IUtxoManager utxoManager
    ) => Ok(await utxoManager.GetBalance(request));

    [HttpPost("batch/utxo-set")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces(typeof(GetUtxoSetResponse))]
    public async Task<IActionResult> GetUtxoSet(
        [FromBody] GetUtxoSetBatchRequest request,
        [FromServices] IUtxoManager utxoManager
    ) => Ok(await utxoManager.GetUtxoSet(request));

    [HttpPost("utxo-set")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces(typeof(GetUtxoSetResponse))]
    public async Task<IActionResult> GetUtxoSet(
        [FromBody] GetUtxoSetRequest request,
        [FromServices] IUtxoManager utxoManager
    )
    {
        var addressSpecified = request.Address is not (null or "");
        var tokenSpecified = request.TokenId is not (null or "");

        if (!addressSpecified && !tokenSpecified)
            return BadRequest("Address or TokenId must be specified, or both");

        return Ok(await utxoManager.GetUtxoSet(request));
    }

    [HttpPost("history")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces(typeof(AddressHistoryResponse))]
    public async Task<IActionResult> GetDetailedHistory(
        [FromBody] GetAddressHistoryRequest request,
        [FromServices] IAddressHistoryService addressHistoryService
    ) => Ok(await addressHistoryService.GetHistory(request));
}
