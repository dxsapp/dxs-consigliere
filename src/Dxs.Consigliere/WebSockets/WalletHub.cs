using Dxs.Bsv;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Raven.Client.Documents;

namespace Dxs.Consigliere.WebSockets;

public class WalletHub(
    IConnectionManager connectionManager,
    IPublisher publisher,
    ILogger<WalletHub> logger
) : Hub<IWalletHub>, IWalletServer
{
    public static string Route => "/ws/consigliere";

    public Task SubscribeToTransactionStream(SubscribeToTransactionStreamRequest request)
        => connectionManager.SubscribeToTransactionStream(Context.ConnectionId, request.Address);

    public Task UnsubscribeToTransactionStream(SubscribeToTransactionStreamRequest request)
        => connectionManager.UnsubscribeToTransactionStream(Context.ConnectionId, request.Address);

    #region Address

    public Task<List<BalanceDto>> GetBalance(BalanceRequest request, [FromServices] IUtxoManager utxoManager)
        => utxoManager.GetBalance(request);

    public Task<AddressHistoryResponse> GetHistory(
        GetAddressHistoryRequest request,
        [FromServices] IAddressHistoryService addressHistoryService
    ) => addressHistoryService.GetHistory(request);

    public Task<GetUtxoSetResponse> GetUtxoSet(GetUtxoSetRequest request, [FromServices] IUtxoManager utxoManager)
        => utxoManager.GetUtxoSet(request);

    #endregion

    #region Transactions

    public async Task<Dictionary<string, string>> GetTransactions(List<string> ids, [FromServices] IDocumentStore store)
    {
        const int maxTxCount = 100;

        if (ids?.Any() != true)
            throw new Exception("No transaction ids provided");

        var checkedIds = new List<string>();

        foreach (var id in ids)
        {
            if (checkedIds.Count == maxTxCount)
                throw new Exception($"Too much transactions ids in request, max {maxTxCount}");

            if (id.Length != 64 || id.Any(x => !HexConverter.IsHexChar(x)))
                throw new Exception($"Malformed transaction id: \"{id}\"");

            checkedIds.Add(id);
        }

        using var session = store.GetNoCacheNoTrackingSession();
        
        var transactions = await session.LoadAsync<TransactionHexData>(checkedIds.Select(TransactionHexData.GetId));
        var result = transactions
            .ToDictionary(
                x => TransactionHexData.Parse(x.Key),
                x => x.Value is { } mtx
                    ? mtx.Hex
                    : string.Empty
            );

        return result;
    }

    public async Task<bool> Broadcast(string transaction, [FromServices] IBroadcastService broadcastService)
    {
        var result = await broadcastService.Broadcast(transaction);

        return result.Success;
    }

    #endregion

}