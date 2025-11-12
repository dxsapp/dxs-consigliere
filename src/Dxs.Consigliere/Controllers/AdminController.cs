using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Extensions;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Documents;

namespace Dxs.Consigliere.Controllers;

[Route("api/admin")]
public class AdminController: BaseController
{
    [HttpPost("manage/address")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ManageAddress(
        [FromBody] WatchAddressRequest request,
        [FromServices] IDocumentStore documentStore,
        [FromServices] ITransactionFilter transactionFilter
    )
    {
        if (!Address.TryParse(request.Address, out var address))
            return BadRequest($"Unable to parse Address: \"{request.Address}\"");

        try
        {
            var watchingAddress = new WatchingAddress
            {
                Address = address.Value,
                Name = request.Name,
            };

            if (await documentStore.AddEntity(watchingAddress))
            {
                transactionFilter.ManageUtxoSetForAddress(address);
            }

            // await EnqueueDownloadHistory(new AddressBaseRequest(address.Value), session);
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
        [FromServices] IDocumentStore documentStore,
        [FromServices] ITransactionFilter transactionFilter
    )
    {
        if (!TokenId.TryParse(request.TokenId, Network.Mainnet, out var tokenId))
            return BadRequest($"Unable to parse TokenId: \"{request.TokenId}\"");

        try
        {
            var watchingToken = new WatchingToken
            {
                TokenId = tokenId.Value,
                Symbol = request.Symbol,
            };

            if (await documentStore.AddEntity(watchingToken))
            {
                transactionFilter.ManageUtxoSetForToken(tokenId);
            }
        }
        catch (Exception exception)
        {
            return InternalError(exception.Message);
        }

        return Ok();
    }
}