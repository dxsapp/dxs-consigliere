using System.ComponentModel.DataAnnotations;

using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Services;

using Microsoft.AspNetCore.Mvc;

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

    [HttpPost("broadcast/{raw}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces(typeof(Broadcast))]
    public async Task<IActionResult> Broadcast(
        string raw,
        [FromServices] IBroadcastService broadcastService
    ) => Ok(await broadcastService.Broadcast(raw));

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
