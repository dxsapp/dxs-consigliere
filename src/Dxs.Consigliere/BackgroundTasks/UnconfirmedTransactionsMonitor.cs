using ComposableAsync;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Rpc.Models;
using Dxs.Bsv.Rpc.Services;
using Dxs.Common.BackgroundTasks;
using Dxs.Common.Time;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;
using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.WoC;
using Dxs.Infrastructure.WoC.Dto;
using Microsoft.Extensions.Options;
using RateLimiter;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.BackgroundTasks;

public class DisappearedFromChain
{
    public string Id { get; set; }
    public string TxId { get; set; }
}

public class UnconfirmedTransactionsMonitor(
    IServiceProvider serviceProvider,
    IWhatsOnChainRestApiClient wocClient,
    IBitailsRestApiClient bitailsRestApi,
    IBlockMessageBus blockMessageBus,
    IRpcClient rpcClient,
    IOptions<AppConfig> appConfig,
    ILogger<UnconfirmedTransactionsMonitor> logger
): PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    private readonly ILogger _logger = logger;

    private static readonly TimeLimiter RateLimiter = TimeLimiter.GetFromMaxCountByInterval(1, TimeSpan.FromSeconds(1));

    protected override TimeSpan Period => TimeSpan.FromMinutes(5);
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromMinutes(1);

    public override string Name => nameof(UnconfirmedTransactionsMonitor);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var store = serviceProvider.GetRequiredService<IDocumentStore>();
        
        using var _ = _logger.BeginScope("UnconfirmedTransactionsMonitor.Run: {StartedAt}", DateTime.UtcNow.ToUnixSeconds());
        using var session = store.GetSession();

        var foundBefore = (DateTime.UtcNow - TimeSpan.FromMinutes(20)).ToUnixSeconds();
        var txs = new List<MetaTransaction>();
        var query = session
            .Query<MetaTransaction>()
            .Where(x => x.Block == null)
            .Where(x => x.Timestamp < foundBefore)
            .OrderBy(x => x.Timestamp);

        var results = await session.Advanced.StreamAsync(query, cancellationToken);

        while (await results.MoveNextAsync())
        {
            var tx = results.Current.Document;

            txs.Add(tx);
        }

        if (!txs.Any())
        {
            _logger.LogDebug("No outdated transaction found");

            return;
        }

        _logger.LogDebug("Found {Count} not mined for too long transactions", txs.Count);

        var totalMissingTxs = 0;
        var processedTxs = 0;

        const int maxBatch = 20;
        var allMessingBlocks = new HashSet<string>();

        while (true)
        {
            var blocksToSync = new HashSet<string>();
            var txIds = new List<string>();
            var missingTxs = new List<string>();
            var l = Math.Min(processedTxs + maxBatch, txs.Count);

            for (; processedTxs < l; processedTxs++)
            {
                txIds.Add(txs[processedTxs].Id);
            }

            if (txIds.Any())
            {
                _logger.LogDebug("Processing {Txs}", txIds);

                await RateLimiter; // additional limiter 

                var wocResp = await wocClient.GetTransactionsAsync(txIds, cancellationToken);

                _logger.LogDebug(
                    "Woc Response {@WoCResponse}",
                    wocResp.Select(x =>
                        new TransactionDetailsSlimDto
                        {
                            BlockHash = x.BlockHash,
                            BlockHeight = x.BlockHeight,
                            BlockTime = x.BlockTime,
                            Hex = x.Hex != null
                                ? new string(x.Hex.Take(500).ToArray())
                                : null,
                            Id = x.Id
                        })
                );

                foreach (var txId in txIds)
                {
                    var detailsDto = wocResp.FirstOrDefault(x => x.Id == txId);

                    if (detailsDto == null || string.IsNullOrEmpty(detailsDto.Hex))
                    {
                        missingTxs.Add(txId);
                        totalMissingTxs++;

                    }
                    else
                    {
                        if (detailsDto.BlockHash != null && allMessingBlocks.Add(detailsDto.BlockHash))
                        {
                            blocksToSync.Add(detailsDto.BlockHash);
                        }
                    }
                }
            }

            if (missingTxs.Any())
            {
                _logger.LogDebug("Missing Txs {@MissingTxIds}", missingTxs);

                using var session2 = store.GetSession();
                var datas = await session2
                    .LoadAsync<TransactionHexData>(missingTxs.Select(TransactionHexData.GetId), cancellationToken);

                foreach (var data in datas)
                {
                    try
                    {
                        var response = await rpcClient.SendRawTransaction(data.Value.Hex).EnsureSuccess();

                        _logger.LogDebug("Transaction re-broadcasted to Node: {TxId}: {@Response}", data.Value.TxId, response);
                    }
                    catch (Exception nodeException)
                    {
                        if (nodeException.Message.Contains("Transaction already in the mempool", StringComparison.InvariantCultureIgnoreCase))
                        {
                            try
                            {
                                await bitailsRestApi.Broadcast(data.Value.Hex, CancellationToken.None);
                            }
                            catch (Exception exception)
                            {
                                _logger.LogError(exception, "Error broadcasting transaction to bitails {TxId}", data.Value.TxId);
                            }
                        }

                        _logger.LogDebug("Failed re-broadcast to Node: {TxId}: {Error}", data.Value.TxId, nodeException.Message);

                        await session2.StoreAsync(
                            new DisappearedFromChain
                            {
                                TxId = data.Value.TxId,
                            },
                            cancellationToken
                        );
                    }
                }

                await session2.SaveChangesAsync(cancellationToken);
            }

            _logger.LogDebug("Processed {Count} txs, {Remain} more", txIds.Count, txs.Count - processedTxs);

            if (blocksToSync.Any())
            {
                _logger.LogDebug("Blocks to sync: {@BlocksToSync}", blocksToSync);

                foreach (var hash in blocksToSync)
                {
                    blockMessageBus.Post(new BlockMessage(hash));
                }
            }

            if (processedTxs >= txs.Count)
                break;
        }

        if (totalMissingTxs > 0)
            _logger.LogDebug("Total transactions was not in mempool {Count}", totalMissingTxs);

        _logger.LogDebug("Finished");
    }

    private async Task SyncDisappeared(CancellationToken cancellationToken)
    {
        var store = serviceProvider.GetRequiredService<IDocumentStore>();

        using var session = store.GetSession();

        var gones = await session
            .Query<DisappearedFromChain>()
            .ToListAsync(token: cancellationToken);
        var txIds = gones.Select(x => x.TxId);

        var txs = await session
            .Query<MetaTransaction>()
            .Where(x => x.Id.In(txIds))
            .OrderBy(x => x.Timestamp)
            .ToListAsync(token: cancellationToken);

        var datas = await session
            .LoadAsync<TransactionHexData>(txs.Select(x => TransactionHexData.GetId(x.Id)), cancellationToken);

        foreach (var data in datas)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var gone = gones.First(x => x.TxId == data.Value.TxId);

            try
            {
                var broadcasted = await bitailsRestApi.IsBroadcastedAsync(data.Value.TxId, cancellationToken);

                if (broadcasted)
                {
                    _logger.LogDebug("Found on Bitails: {TxId}", data.Value.TxId);
                    session.Delete(gone);
                    continue;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to get tx from Bitails");
            }

            try
            {
                var response = await rpcClient.SendRawTransaction(data.Value.Hex).EnsureSuccess();

                _logger.LogDebug("Transaction re-broadcasted to Node: {TxId}: {@Response}", data.Value.TxId, response);

                session.Delete(gone);
            }
            catch (Exception nodeException)
            {
                if (nodeException.Message == "Request failed: (code: -27, message: Transaction already in the mempool)"
                    || nodeException.Message == "Request failed: (code: -26, message: 257: txn-already-known)")
                {
                    session.Delete(gone);
                }

                _logger.LogDebug("Failed re-broadcast to Node: {TxId}: {Error}", data.Value.TxId, nodeException.Message);

                var tx = txs.First(x => x.Id == data.Value.TxId);

                foreach (var input in tx.Inputs)
                {
                    if (gones.All(x => x.TxId != input.TxId))
                    {
                        await session.StoreAsync(new DisappearedFromChain { TxId = input.TxId }, cancellationToken);
                    }
                }

                continue;

                try
                {
                    await wocClient.BroadcastAsync(data.Value.Hex, cancellationToken);
                    session.Delete(gone);

                    _logger.LogDebug("Transaction re-broadcasted to WoC: {TxId}", data.Value.TxId);
                }
                catch (Exception wocException)
                {
                    if (wocException.Message ==
                        "Response code validation failed POST https://api.whatsonchain.com/v1/bsv/main/tx/raw\nResponse: BadRequest \"unexpected response code 500: Transaction already in the mempool\"")
                    {
                        session.Delete(gone);
                    }

                    _logger.LogDebug("Failed re-broadcast to Woc: {TxId}: {Error}", data.Value.TxId, wocException.Message);
                }
            }
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}