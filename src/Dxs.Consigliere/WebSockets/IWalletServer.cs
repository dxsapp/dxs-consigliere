using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Services;

using Raven.Client.Documents;

namespace Dxs.Consigliere.WebSockets;

public interface IWalletServer
{
    #region Address

    Task<List<BalanceDto>> GetBalance(
        BalanceRequest request,
        ITrackedEntityReadinessService readinessService,
        IUtxoManager utxoManager
    );

    Task<AddressHistoryResponse> GetHistory(
        GetAddressHistoryRequest request,
        ITrackedEntityReadinessService readinessService,
        IAddressHistoryService addressHistoryService
    );

    Task<GetUtxoSetResponse> GetUtxoSet(
        GetUtxoSetRequest request,
        ITrackedEntityReadinessService readinessService,
        IUtxoManager utxoManager
    );

    #endregion

    #region Transactions

    Task<Dictionary<string, string>> GetTransactions(List<string> ids, IDocumentStore store);

    /// <summary>
    /// Wave 5 S1 — unified broadcast hub method. Returns the frozen
    /// <see cref="BroadcastReceiptDto"/> (W1 S0.8 contract freeze).
    /// Previously a <c>Task&lt;bool&gt;</c> from the legacy HTTP-provider
    /// path AND a separate <c>BroadcastTracked</c> method for the P2P
    /// path; both collapsed into this signature.
    /// </summary>
    Task<BroadcastReceiptDto> Broadcast(string rawHex, IBroadcastService broadcastService);

    #endregion

    #region Block tip (Wave 1)

    /// <summary>
    /// Wave 1 S0.8: add caller's connection to the `block:tip` group so it
    /// receives <see cref="IWalletHub.OnNewBlock"/> notifications.
    /// </summary>
    Task SubscribeToBlockTip();

    /// <summary>
    /// Wave 1 S0.8: add caller's connection to the `block:reorg` group so
    /// it receives <see cref="IWalletHub.OnReorg"/> notifications. No
    /// messages are sent on this group in W1 — Wave 3 wires the emitter.
    /// </summary>
    Task SubscribeToReorg();

    #endregion
}
