using System.ComponentModel.DataAnnotations;
using System.Threading;

using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.WebSockets;

using Dxs.Consigliere.Setup;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dxs.Consigliere.Controllers;

[Route("api/tx")]
public class TransactionController : BaseController
{
    [HttpGet("get/{id}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces(typeof(string))]
    public async Task<IActionResult> GetTransaction(
        string id,
        [FromServices] ITransactionQueryService transactionQueryService
    )
    {
        try
        {
            return Ok(await transactionQueryService.GetTransactionAsync(id));
        }
        catch (TransactionQueryException exception)
        {
            return MapTransactionQueryException(exception, includeNotFoundBody: true);
        }
    }

    [HttpGet("state/{id}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces(typeof(TransactionStateResponse))]
    public async Task<IActionResult> GetTransactionState(
        string id,
        [FromServices] ITransactionQueryService transactionQueryService,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Ok(await transactionQueryService.GetTransactionStateAsync(id, cancellationToken));
        }
        catch (TransactionQueryException exception)
        {
            return MapTransactionQueryException(exception, includeNotFoundBody: true);
        }
    }

    [HttpGet("batch/get")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces(typeof(Dictionary<string, string>))]
    public async Task<IActionResult> GetTransactions(
        [Required][FromQuery] List<string> ids,
        [FromServices] ITransactionQueryService transactionQueryService
    )
    {
        try
        {
            return Ok(await transactionQueryService.GetTransactionsAsync(ids));
        }
        catch (TransactionQueryException exception)
        {
            return MapTransactionQueryException(exception, includeNotFoundBody: true);
        }
    }

    [HttpGet("by-height/get")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces(typeof(GetTransactionsByBlockResponse))]
    public async Task<IActionResult> GetTransactionsByBlock(
        [Required][FromQuery] int blockHeight,
        [Required][FromQuery] int skip,
        [FromServices] ITransactionQueryService transactionQueryService,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Ok(await transactionQueryService.GetTransactionsByBlockAsync(
                blockHeight,
                skip,
                cancellationToken
            ));
        }
        catch (TransactionQueryException exception)
        {
            return MapTransactionQueryException(exception, includeNotFoundBody: true);
        }
    }

    /// <summary>
    /// Wave 5 S1 — unified broadcast endpoint. Replaces the legacy
    /// <c>POST /api/tx/broadcast/{raw}</c> (which used the
    /// multi-provider HTTP path and returned a Raven <c>Broadcast</c>
    /// document); the new shape accepts a JSON body and returns the
    /// frozen <see cref="BroadcastReceiptDto"/> shape. External
    /// wallet clients must migrate — see W5 closeout MIGRATION
    /// snippet.
    /// </summary>
    [HttpPost("broadcast")]
    [EnableRateLimiting(RateLimiterPolicies.BroadcastPolicy)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [Produces(typeof(BroadcastReceiptDto))]
    public async Task<IActionResult> Broadcast(
        [FromBody] BroadcastTxRequest body,
        [FromServices] IBroadcastService broadcastService,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrEmpty(body.RawHex))
            return BadRequest(new { error = "rawHex required" });

        var receipt = await broadcastService.BroadcastAsync(
            body.RawHex, clientConnectionId: null, cancellationToken);
        return Ok(new BroadcastReceiptDto(
            receipt.TxId, receipt.State.ToString(), receipt.CreatedAtMs, receipt.FailReason));
    }

    // W5 A2 L4 fix: BroadcastTxRequest moved to
    // src/Dxs.Consigliere/Dto/Requests/BroadcastTxRequest.cs alongside
    // the other request DTOs (repo convention).

    [HttpGet("stas/validate/{id}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(418)]
    [Produces(typeof(ValidateStasResponse))]
    public async Task<IActionResult> ValidateStasTransaction(
        string id,
        [FromServices] ITransactionQueryService transactionQueryService
    )
    {
        try
        {
            return Ok(await transactionQueryService.ValidateStasTransactionAsync(id));
        }
        catch (TransactionQueryException exception)
        {
            return MapTransactionQueryException(exception, includeNotFoundBody: false);
        }
    }

    private IActionResult MapTransactionQueryException(
        TransactionQueryException exception,
        bool includeNotFoundBody
    )
        => exception.Kind switch
        {
            TransactionQueryErrorKind.BadRequest => BadRequest(exception.Message),
            TransactionQueryErrorKind.NotFound when includeNotFoundBody => NotFound(exception.Message),
            TransactionQueryErrorKind.NotFound => NotFound(),
            TransactionQueryErrorKind.NotStas => StatusCode(418, exception.Message),
            _ => InternalError(exception.Message),
        };
}
