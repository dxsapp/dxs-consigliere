using Dxs.Bsv;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Dto.Responses.Readiness;
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

    public Task SubscribeToTokenStream(
        SubscribeToTokenStreamRequest request,
        [FromServices] INetworkProvider networkProvider
    ) => connectionManager.SubscribeToTokenStream(
        Context.ConnectionId,
        request.TokenId.EnsureValidTokenId(networkProvider.Network).Value
    );

    public Task UnsubscribeToTokenStream(
        SubscribeToTokenStreamRequest request,
        [FromServices] INetworkProvider networkProvider
    ) => connectionManager.UnsubscribeToTokenStream(
        Context.ConnectionId,
        request.TokenId.EnsureValidTokenId(networkProvider.Network).Value
    );

    public Task SubscribeToDeletedTransactionStream()
        => connectionManager.SubscribeToDeletedTransactionStream(Context.ConnectionId);

    public Task UnsubscribeToDeletedTransactionStream()
        => connectionManager.UnsubscribeToDeletedTransactionStream(Context.ConnectionId);

    #region Address

    public async Task<List<BalanceDto>> GetBalance(
        BalanceRequest request,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] IUtxoManager utxoManager
    )
    {
        await EnsureReadableAsync(request.Addresses ?? [], request.TokenIds ?? [], readinessService);
        return await utxoManager.GetBalance(request);
    }

    public async Task<AddressHistoryResponse> GetHistory(
        GetAddressHistoryRequest request,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] IAddressHistoryService addressHistoryService
    )
    {
        await EnsureReadableAsync([request.Address], request.TokenIds ?? [], readinessService);
        return await addressHistoryService.GetHistory(request);
    }

    public async Task<GetUtxoSetResponse> GetUtxoSet(
        GetUtxoSetRequest request,
        [FromServices] ITrackedEntityReadinessService readinessService,
        [FromServices] IUtxoManager utxoManager
    )
    {
        var addresses = string.IsNullOrWhiteSpace(request.Address) ? [] : new[] { request.Address };
        var tokenIds = string.IsNullOrWhiteSpace(request.TokenId) ? [] : new[] { request.TokenId };

        await EnsureReadableAsync(addresses, tokenIds, readinessService);
        return await utxoManager.GetUtxoSet(request);
    }

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

    /// <summary>
    /// Wave 5 S1 — unified broadcast hub method. Replaces the legacy
    /// <c>Broadcast(string)</c> (which returned <c>bool</c> via the
    /// multi-provider HTTP path) AND the W2 <c>BroadcastTracked</c>
    /// transitional method (which returned the same DTO this method
    /// now returns). External wallet clients calling either legacy
    /// name will see runtime errors — see W5 closeout MIGRATION
    /// snippet. State transitions stream via OnBroadcastStateChanged
    /// to the caller's connection group.
    /// </summary>
    public async Task<BroadcastReceiptDto> Broadcast(
        string rawHex,
        [FromServices] IBroadcastService broadcastService)
    {
        var receipt = await broadcastService.BroadcastAsync(rawHex, BroadcastSource.Wallet, Context.ConnectionId);
        return new BroadcastReceiptDto(receipt.TxId, receipt.State.ToString(), receipt.CreatedAtMs, receipt.FailReason);
    }

    /// <summary>Subscribe to state-change events for one txid.</summary>
    public Task SubscribeToBroadcast(string txId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"broadcast:{txId}");

    public Task UnsubscribeFromBroadcast(string txId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"broadcast:{txId}");

    #endregion

    #region Block tip (Wave 1)

    /// <summary>
    /// Wave 1 S0.8: subscribe caller to <c>block:tip</c>. The actual
    /// broadcast to that group lives in S5's <c>HubNewBlockNotifier</c>
    /// via <c>IHubContext&lt;WalletHub, IWalletHub&gt;</c> — this is the
    /// server method, not the emitter.
    /// </summary>
    public Task SubscribeToBlockTip()
        => Groups.AddToGroupAsync(Context.ConnectionId, "block:tip");

    /// <summary>
    /// Wave 1 S0.8: subscribe caller to <c>block:reorg</c>. Emitter wired
    /// in Wave 3; no messages are sent on this group in W1.
    /// </summary>
    public Task SubscribeToReorg()
        => Groups.AddToGroupAsync(Context.ConnectionId, "block:reorg");

    #endregion

    private static async Task EnsureReadableAsync(
        IEnumerable<string> addresses,
        IEnumerable<string> tokenIds,
        ITrackedEntityReadinessService readinessService
    )
    {
        var gate = await readinessService.GetBlockingReadinessAsync(addresses, tokenIds);
        if (gate is null)
            return;

        throw new InvalidOperationException(BuildReadinessMessage(gate));
    }

    private static string BuildReadinessMessage(TrackedEntityReadinessGateResponse gate)
        => gate.Code switch
        {
            "not_tracked" => "tracked scope is required before reading this stream",
            "scope_not_ready" => "tracked scope is not live yet",
            _ => "tracked scope is not readable"
        };
}
