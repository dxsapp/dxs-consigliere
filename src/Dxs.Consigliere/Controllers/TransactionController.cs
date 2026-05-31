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

    /// <summary>
    /// Demo aid: ask a public explorer whether it has seen this txid yet.
    /// The Broadcast inspector polls one source per second until seen, to
    /// independently prove a P2P-broadcast tx reached third-party indexers.
    /// `source` ∈ { woc, bitails, junglebus }. Provider errors / not-found
    /// return seen=false so the caller simply polls again.
    /// </summary>
    [HttpGet("{id}/external-sighting/{source}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces(typeof(ExternalSightingResponse))]
    public async Task<IActionResult> GetExternalSighting(
        string id,
        string source,
        [FromServices] Dxs.Infrastructure.WoC.IWhatsOnChainRestApiClient woc,
        [FromServices] Dxs.Infrastructure.Bitails.IBitailsRestApiClient bitails,
        [FromServices] Dxs.Infrastructure.JungleBus.IJungleBusRawTransactionClient jungleBus,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length != 64)
            return BadRequest("invalid txid");

        var src = (source ?? string.Empty).ToLowerInvariant();
        bool seen;
        try
        {
            seen = src switch
            {
                "woc" or "whatsonchain" => await woc.IsBroadcastedAsync(id, cancellationToken),
                "bitails" => await bitails.IsBroadcastedAsync(id, cancellationToken),
                "junglebus" => (await jungleBus.GetTransactionRawOrNullAsync(id, cancellationToken)) is { Length: > 0 },
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest("unknown source (expected: woc | bitails | junglebus)");
        }
        catch
        {
            // Provider hiccup or not-yet-indexed — report not seen; the
            // inspector polls again on its next tick.
            seen = false;
        }

        return Ok(new ExternalSightingResponse { Source = src, Seen = seen });
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

        // S3-followup-2: `/api/tx/broadcast` is a shared endpoint —
        // the admin UI hits it with a cookie (authenticated), but
        // external wallet API clients hit it anonymously. The audit
        // `source` is derived from the resolved principal so the
        // forensic record distinguishes the two.
        var source = User?.Identity?.IsAuthenticated == true
            ? BroadcastSource.Operator
            : BroadcastSource.Api;
        var receipt = await broadcastService.BroadcastAsync(
            body.RawHex, source, clientConnectionId: null, cancellationToken);
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
