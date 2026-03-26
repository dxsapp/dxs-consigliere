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

    Task<bool> Broadcast(string transaction, IBroadcastService broadcastService);

    #endregion
}
