using System.Threading;
using System.Threading.Tasks;

using Dxs.Infrastructure.Bitails.Dto;

namespace Dxs.Infrastructure.Bitails;

public interface IBitailsRestApiClient
{
    Task<HistoryPage> GetHistoryPageAsync(string address, string pgKey, int limit, CancellationToken token);
    Task<AddressDetailsDto> GetAddressDetailsAsync(string address, CancellationToken token = default);
    // W5 S4: Broadcast(string txHex, ct) deleted. Tx submission moved to
    // IBroadcastService.BroadcastAsync (the unified P2P path). Bitails
    // remains a non-broadcast source of address / tx / token data only.
    Task<bool> IsBroadcastedAsync(string txId, CancellationToken token = default);
    Task<byte[]> GetTransactionRawOrNullAsync(string txId, CancellationToken token = default);
    Task<TransactionDetailsDto> GetTransactionDetails(string txId, CancellationToken token = default);
    Task<OutputDetailsDto> GetOutputDetails(string txId, int vout, CancellationToken token = default);
    Task<TokenDetailsDto> GetTokenDetails(string tokenId, string symbol, CancellationToken token = default);
}
