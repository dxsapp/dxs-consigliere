using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Infrastructure.WoC.Dto;

namespace Dxs.Infrastructure.WoC;

// https://developers.whatsonchain.com/
public interface IWhatsOnChainRestApiClient
{
    /// <summary>
    /// check broadcasted transaction
    /// </summary>
    /// <param name="txId">hash</param>
    /// <param name="token">token</param>
    /// <returns>true if broadcasted</returns>
    /// <exception cref="DetailedHttpRequestException">if response is not success status code</exception>
    Task<bool> IsBroadcastedAsync(string txId, CancellationToken token = default);

    /// <summary>
    /// Get raw transaction data in hex.
    /// This endpoint returns raw hex for the transaction with given hash
    /// https://developers.whatsonchain.com/#get-raw-transaction-data
    /// </summary>
    /// <param name="txId">The hash/txId of the transaction</param>
    /// <param name="token">token</param>
    /// <returns>raw transaction data in hex</returns>
    /// <exception cref="DetailedHttpRequestException">if response is not success status code</exception>
    Task<string> GetTransactionRawOrNullAsync(string txId, CancellationToken token = default);

    /// <summary>
    /// Get multiple transactions raw data in hex in a single request
    /// https://developers.whatsonchain.com/#bulk-raw-transaction-data
    /// </summary>
    /// <param name="txIds">The hashes/txIdes of the transactions (Max 20 transactions per request)</param>
    /// <param name="token">token</param>
    /// <returns>multiple transactions raw data in hex</returns>
    Task<IList<TransactionDetailsSlimDto>> GetTransactionsAsync(IEnumerable<string> txIds, CancellationToken token = default);

    /// <summary>
    /// Broadcast transaction using this endpoint. Get tx id in response or error msg from the node with header content-type: text/plain.
    /// https://developers.whatsonchain.com/#broadcast-transaction
    /// </summary>
    /// <param name="body">Raw transaction data in hex</param>
    /// <param name="token">token</param>
    /// <returns></returns>
    Task BroadcastAsync(string body, CancellationToken token = default);

    /// <summary>
    /// Fetch confirmed and unconfirmed balance for multiple addresses in a single request
    /// https://developers.whatsonchain.com/#bulk-balance
    /// </summary>
    /// <param name="addresses">Max 20 addresses per request</param>
    /// <param name="token">token</param>
    /// <returns>balances addresses</returns>
    Task<Dictionary<string, decimal>> GetBalancesAsync(IEnumerable<string> addresses, CancellationToken token = default);

    /// <summary>
    /// Get unspent transactions.
    /// https://developers.whatsonchain.com/#get-unspent-transactions
    /// </summary>
    /// <param name="address">address</param>
    /// <param name="skip">page</param>
    /// <param name="take">page size</param>
    /// <param name="cancellationToken">token</param>
    /// <returns>retrieves ordered list of UTXOs</returns>
    Task<UnspentOutputDto[]> GetUtxosAsync(string address, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// <para>
    /// Get block pages
    /// </para>
    /// <para>
    /// If the block has more that 100 transactions the page URIs will be provided in the pages element when getting a block by hash or height.
    /// </para>
    /// https://developers.whatsonchain.com/#get-block-pages
    /// </summary>
    /// <param name="hash">The hash of the block to retrieve</param>
    /// <param name="number">Page number</param>
    /// <param name="token">token</param>
    /// <returns>hash transactions</returns>
    /// <exception cref="DetailedHttpRequestException">if response is not success status code</exception>
    Task<string[]> GetBlockPagesAsync(string hash, int number, CancellationToken token = default);

    /// <summary>
    /// <para>
    /// This endpoint retrieves the block details of a given block height.
    /// </para>
    /// <para>
    /// For a block with up to 100 transaction, all transaction ids are returned in response to this call.
    /// If a block has more than 100 transactions, only top 100 transaction ids are returned.
    /// To get remaining ids see 'Get block pages' section.
    /// https://developers.whatsonchain.com/#get-by-height
    /// </para>
    /// </summary>
    /// <param name="height">The height of the block to retrieve</param>
    /// <param name="token">token</param>
    /// <returns>block details</returns>
    /// <exception cref="DetailedHttpRequestException">if response is not success status code</exception>
    Task<BlockDto> GetBlockByHeightAsync(int height, CancellationToken token = default);

    Task<TransactionDetailsDto> GetTransactionDetails(string transactionId, CancellationToken token = default);

    Task<TokenDetailsDto> GetTokenDetails(string tokenId, string symbol, CancellationToken token = default);

    Task<SpentTransactionOutput> GetSpentTransactionOutput(string transactionId, ulong vout, CancellationToken token = default);
}
