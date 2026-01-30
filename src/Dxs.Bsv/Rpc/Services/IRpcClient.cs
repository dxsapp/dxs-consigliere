#nullable enable
using System.IO;
using System.Threading.Tasks;

using Dxs.Bsv.Rpc.Models.Responses;

namespace Dxs.Bsv.Rpc.Services;

public interface IRpcClient
{
    /// <summary>
    /// List all commands, or get help for a specified command.
    /// </summary>
    /// <param name="command">The command to get help on (default=all commands)</param>
    /// <returns></returns>
    Task<RpcResponseString> Help(string? command = default);

    /// <summary>
    /// <para>
    ///     "getblock" rpc method (verbosity: 0)
    /// </para>
    /// <para>
    ///     https://developer.bitcoin.org/reference/rpc/getblock.html
    /// </para>
    /// </summary>
    /// <param name="blockHash">The block hash</param>
    /// <returns>hex-encoded data string</returns>
    Task<RpcResponseString> GetBlockAsString(string blockHash);

    /// <summary>
    /// <para>
    ///     "getblock" rpc method (verbosity: 0)
    /// </para>
    /// <para>
    ///     https://developer.bitcoin.org/reference/rpc/getblock.html
    /// </para>
    /// </summary>
    /// <param name="blockHash">The block hash</param>
    /// <returns>Stream of block</returns>
    Task<Stream> GetBlockAsStream(string blockHash);

    /// <summary>
    /// <para>
    ///     getblockcount
    ///     Returns the height of the most-work fully-validated chain.
    ///     The genesis block has height 0.
    /// </para>
    /// <para>
    ///     https://developer.bitcoin.org/reference/rpc/getblockcount.html
    /// </para>
    /// </summary>
    /// <returns>The current block count</returns>
    Task<RpcResponseInt> GetBlockCount();

    /// <summary>
    /// <para>
    ///     getblockhash height
    ///     Returns hash of block in best-block-chain at height provided.
    /// </para>
    /// <para>
    ///     https://developer.bitcoin.org/reference/rpc/getblockhash.html
    /// </para>
    /// </summary>
    /// <param name="height">The height index</param>
    /// <returns>The current block count</returns>
    Task<RpcResponseString> GetBlockHash(int height);

    /// <summary>
    /// <para>
    ///     getblockheader hash (verbose=true)
    /// </para>
    /// <para>
    ///     Returns an Object with information about blockheader
    /// </para>
    /// </summary>
    /// <returns>json object <see cref="RpcGetBlockHeaderResponse"/></returns>
    Task<RpcGetBlockHeaderResponse> GetBlockHeader(string hash);

    /// <summary>
    /// <para>
    ///     getblockchaininfo
    /// </para>
    /// <para>
    ///     Returns an object containing various state info regarding blockchain processing.
    /// </para>
    /// </summary>
    /// <returns></returns>
    Task<RpcGetBlockChainInfoResponse> GetBlockChainInfo();

    /// <summary>
    /// <para>
    ///     getrawmempool (for verbose = true)
    /// </para>
    /// <para>
    ///     Returns all transaction ids in memory pool as a json array of string transaction ids.
    /// </para>
    /// </summary>
    /// <returns>json object <see cref="RpcRawMemPoolResponse"/></returns>
    Task<RpcRawMemPoolResponse> GetRawMemPool();

    /// <summary>
    /// <para>
    ///     sendrawtransaction
    /// </para>
    /// <para>
    ///     Returns all transaction ids in memory pool as a json array of string transaction ids.
    /// </para>
    /// </summary>
    /// <param name="hexRawTx">The hex string of the raw transaction</param>
    /// <param name="allowHighFees">Allow high fees (default: false)</param>
    /// <param name="dontCheckFee">Don't check fee (default: false)</param>
    /// <returns>The transaction hash in hex</returns>
    Task<RpcResponseStringWithErrorDetails> SendRawTransaction(string hexRawTx, bool allowHighFees = false, bool dontCheckFee = false);

    /// <summary>
    /// <para>
    ///     getrawtransaction (for verbose = true)
    /// </para>
    /// <para>
    ///     NOTE: By default this function only works for mempool transactions. If the -txindex option is enabled, it also works for blockchain transactions.
    ///     DEPRECATED: for now, it also works for transactions with unspent outputs.
    /// </para>
    /// </summary>
    /// <param name="txId">The transaction id</param>
    /// <returns>Return the raw transaction data</returns>
    Task<RpcResponseStringWithErrorDetails> GetRawTransactionAsString(string txId);

    /// <summary>
    /// <para>
    ///     getrawtransaction (for verbose = false)
    /// </para>
    /// <para>
    ///     NOTE: By default this function only works for mempool transactions. If the -txindex option is enabled, it also works for blockchain transactions.
    ///     DEPRECATED: for now, it also works for transactions with unspent outputs.
    /// </para>
    /// </summary>
    /// <param name="txId">The transaction id</param>
    /// <returns>Return the raw transaction data</returns>
    Task<RpcGetRawTxResponse> GetRawTransactionAsJsonObject(string txId);


    Task<ChainTipsResponse> GetChainTips();
}
