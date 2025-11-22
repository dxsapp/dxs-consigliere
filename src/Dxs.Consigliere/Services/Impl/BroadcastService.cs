using Dxs.Bsv;
using Dxs.Bsv.Factories;
using Dxs.Bsv.Models;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Extensions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Raven.Client.Documents;

namespace Dxs.Consigliere.Services.Impl;

public class BroadcastService(
    IBitcoindService bitcoindService,
    IDocumentStore documentStore,
    IUtxoCache utxoCache,
    INetworkProvider networkProvider,
    IOptions<AppConfig> appConfig,
    ILogger<BroadcastService> logger
): IBroadcastService
{
    private readonly AppConfig _appConfig = appConfig.Value;

    private readonly AsyncRetryPolicy _simpleRetryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(10, _ => TimeSpan.FromSeconds(2));

    public Task<decimal> SatoshisPerByte() => bitcoindService.SatoshisPerByte();

    public Task<Broadcast> Broadcast(string transaction, string batchId = null)
    {
        if (!Transaction.TryParse(transaction, networkProvider.Network, out var parsed))
            throw new Exception("Unable to parse transaction");

        return Broadcast(parsed, batchId);
    }

    public async Task<Broadcast> Broadcast(Transaction transaction, string batchId = null)
    {
        using var _ = logger.BeginScope("{TransactionId}", transaction.Id);

        var broadcastAttempt = new Broadcast
        {
            TxId = transaction.Id,
            BatchId = batchId
        };
        await documentStore.AddOrUpdateEntity(broadcastAttempt);

        var (success, message, code) = await BroadcastWithRetry(transaction);

        broadcastAttempt.Code = code;
        broadcastAttempt.Message = message;
        broadcastAttempt.Success = success;

        await documentStore.UpdateEntity(broadcastAttempt);

        return broadcastAttempt;
    }

    private async Task<(bool success, string message, string code)> BroadcastWithRetry(Transaction transaction)
    {
        var retryResult = await _simpleRetryPolicy.ExecuteAndCaptureAsync(
            async () =>
            {
                var result = await BroadcastToNode(transaction);
                
                if (!result.success)
                {
                    if (result.message.Contains("missing-inputs", StringComparison.InvariantCultureIgnoreCase)
                        || result.message.Contains("missing inputs", StringComparison.InvariantCultureIgnoreCase)
                        || result.message.Contains("mandatory-script-verify-flag-failed", StringComparison.InvariantCultureIgnoreCase))
                    {
                        logger.LogCritical("{@MissingBlockchainData}", new
                        {
                            TransactionId = transaction.Id,
                            Message = result.message,
                            Code = result.code
                        });
                        
                        return (false, result.message, result.code);
                    }
                    
                    throw new ApplicationException(result.message);
                }

                foreach (var input in transaction.Inputs)
                {
                    var outPoint = new OutPoint(input.TxId, input.Address, null, 0, uint.MaxValue);
                    
                    utxoCache.MarkUsed(outPoint, false);
                }

                return result;
            }
        );

        return retryResult.Result;
    }

    private async Task<(bool success, string message, string code)> BroadcastToNode(Transaction transaction)
    {
        var result = await bitcoindService.Broadcast(transaction.Raw.ToHexString());
        
        if (!result.success)
        {
            logger.LogError("{@Broadcast}", new
            {
                TransactionId = transaction.Id,
                Provider = "Node",
                Message = result.message,
                Code = result.code,
            });
        }
        else
        {
            logger.LogDebug("{@Broadcast}", new
            {
                TransactionId = transaction.Id,
                Provider = "Node",
            });
        }

        return result;
    }
}