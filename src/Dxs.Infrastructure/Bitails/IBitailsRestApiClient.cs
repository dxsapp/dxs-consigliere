using System.Threading;
using System.Threading.Tasks;

using Dxs.Infrastructure.Bitails.Dto;

namespace Dxs.Infrastructure.Bitails;

public interface IBitailsRestApiClient
{
    Task<HistoryPage> GetHistoryPageAsync(string address, string pgKey, int limit, CancellationToken token);
    Task<AddressDetailsDto> GetAddressDetailsAsync(string address, CancellationToken token = default);
    Task<BroadcastResponseDto> Broadcast(string txHex, CancellationToken token);
    Task<bool> IsBroadcastedAsync(string txId, CancellationToken token = default);
    Task<byte[]> GetTransactionRawOrNullAsync(string txId, CancellationToken token = default);
    Task<TransactionDetailsDto> GetTransactionDetails(string txId, CancellationToken token = default);
    Task<OutputDetailsDto> GetOutputDetails(string txId, int vout, CancellationToken token = default);
    Task<TokenDetailsDto> GetTokenDetails(string tokenId, string symbol, CancellationToken token = default);
}
