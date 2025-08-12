using System.ComponentModel.DataAnnotations;
using Dxs.Bsv;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Documents;

namespace Dxs.Consigliere.Controllers;

[Route("api/tx")]
public class TransactionController: BaseController
{
    [HttpGet("get/{id}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces(typeof(string))]
    public async Task<IActionResult> GetTransaction(
        string id,
        [FromServices] IDocumentStore store
    )
    {
        if (id.Length != 64 || id.Any(x => !HexConverter.IsHexChar(x)))
            return BadRequest("Malformed transaction id");

        using var session = store.GetNoCacheNoTrackingSession();
        var transaction = await session.LoadAsync<TransactionHexData>(TransactionHexData.GetId(id));

        return transaction == null
            ? NotFound("Not found")
            : transaction.Hex?.Length > 0
                ? Ok(transaction.Hex)
                : InternalError("Missing transaction data");
    }

    [HttpGet("get/batch")]
    [HttpGet("batch/get")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces(typeof(Dictionary<string, string>))]
    public async Task<IActionResult> GetTransactions(
        [Required] [FromQuery] List<string> ids,
        [FromServices] IDocumentStore store
    )
    {
        const int maxTxCount = 1000;

        if (ids == null)
            return BadRequest("Ids not specified");

        var checkedIds = new List<string>();

        foreach (var id in ids)
        {
            if (checkedIds.Count == maxTxCount)
                return BadRequest($"Too much transactions ids in request, max {maxTxCount}");

            if (id.Length != 64 || id.Any(x => !HexConverter.IsHexChar(x)))
                return BadRequest($"Malformed transaction id: \"{id}\"");

            checkedIds.Add(id);
        }

        using var session = store.GetNoCacheNoTrackingSession();
        var transactions = await session.LoadAsync<TransactionHexData>(checkedIds.Select(TransactionHexData.GetId));
        var result = transactions
            .ToDictionary(
                x => TransactionHexData.Parse(x.Key),
                x => x.Value is {} mtx
                    ? mtx.Hex
                    : string.Empty
            );

        return Ok(result);
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
        [FromServices] IDocumentStore store
    )
    {
        if (id.Length != 64 || id.Any(x => !HexConverter.IsHexChar(x)))
            return BadRequest("Malformed transaction id");

        using var session = store.GetNoCacheNoTrackingSession();
        var metaTransaction = await session.LoadAsync<MetaTransaction>(id);

        if (metaTransaction == null)
            return NotFound();

        var askLater = !metaTransaction.IsIssue && !metaTransaction.AllStasInputsKnown;

        if (!askLater && !metaTransaction.IsStas)
            return StatusCode(418, "This is not a STAS transaction");

        return Ok(new ValidateStasResponse(
            askLater,
            metaTransaction.Id,
            metaTransaction.IllegalRoots.Count == 0,
            metaTransaction.IsIssue,
            metaTransaction.IsRedeem,
            metaTransaction.TokenIds.First(),
            [], // metaTransaction.Roots.ToArray(),
            metaTransaction.IllegalRoots.ToArray()
        ));
    }
}