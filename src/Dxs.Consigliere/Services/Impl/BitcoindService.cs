using Dxs.Bsv;
using Dxs.Bsv.Block;
using Dxs.Bsv.Models;
using Dxs.Bsv.Rpc.Models;
using Dxs.Bsv.Rpc.Services;
using Microsoft.Extensions.Logging;

namespace Dxs.Consigliere.Services.Impl;

public class BitcoindService(IRpcClient rpcClient, ILogger<BitcoindService> logger): IBitcoindService
{
    public string Name => "Rpc";

    public Task<decimal> SatoshisPerByte() => Task.FromResult(0.05m);

    public async Task<(bool success, string message, string code)> Broadcast(string hex)
    {
        try
        {
            var result = await rpcClient.SendRawTransaction(hex);

            if (result.Error is {} error)
                return (success: false, error.Message, error.Code.ToString());

            logger.LogDebug("Broadcasted: {TxId}", result.RequestId);

            return (success: true, message: null, code: null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast transaction: {@Tx}", hex);

            return (success: false, message: ex.Message, code: null);
        }
    }

    public async Task<BlockReader> GetRawBlockFromTheTop(int count)
    {
        try
        {
            var height = await rpcClient.GetBlockCount().EnsureSuccess();
            var blockId = await rpcClient.GetBlockHash(height - count).EnsureSuccess();
            var block = await rpcClient.GetBlockAsStream(blockId);

            var blockReader = BlockReader.Parse(block, Network.Mainnet);

            return blockReader;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed");
        }

        return null;
    }

    public async Task<IList<Transaction>> GetMempoolTransactions()
    {
        try
        {
            var memPoolTransactions = await rpcClient.GetRawMemPool().EnsureSuccess();
            var result = new List<Transaction>();
            var txInBlock = new Dictionary<string, int>(); //  txId, index in mempool
            var memoPoolIdx = 0;

            foreach (var (txId, _) in memPoolTransactions)
            {
                var (tx, notInMempool) = await FetchTransactionFromMempool(txId);

                result.Add(tx);

                if (notInMempool)
                {
                    txInBlock.Add(txId, memoPoolIdx++);
                }
            }

            var c = 0;

            while (txInBlock.Any())
            {
                using var block = await GetRawBlockFromTheTop(c++);

                if (block != null)
                {
                    foreach (var tx in block.Transactions())
                    {
                        if (txInBlock.Remove(tx.Id, out var idx))
                        {
                            result[idx] = tx;
                        }
                    }
                }
            }

            return result;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed go fetch transactions from mempool");
            throw;
        }
    }

    private async Task<(Transaction tx, bool notInMempool)> FetchTransactionFromMempool(string transactionId)
    {
        var response = await rpcClient.GetRawTransactionAsString(transactionId);

        if (response.Error is not null)
        {
            logger.LogWarning("Transaction fetch error: {@Error}", response.Error);

            if (response.Error.Message ==
                "No such mempool transaction. Use -txindex to enable blockchain transaction queries. Use gettransaction for wallet transactions.")
            {
                return (null, true);
            }

            return (null, false);
        }

        var tx = Transaction.Parse(response.Result, Network.Mainnet);

        return (tx, false);
    }
}