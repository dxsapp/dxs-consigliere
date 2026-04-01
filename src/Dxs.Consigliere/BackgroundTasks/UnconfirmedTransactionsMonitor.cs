using ComposableAsync;

using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.BackgroundTasks;
using Dxs.Common.Time;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;
using Dxs.Infrastructure.WoC;
using Dxs.Infrastructure.WoC.Dto;

using Microsoft.Extensions.Options;

using RateLimiter;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.BackgroundTasks;

public class UnconfirmedTransactionsMonitor(
    IServiceProvider serviceProvider,
    IWhatsOnChainRestApiClient wocClient,
    IBlockMessageBus blockMessageBus,
    IBroadcastService broadcastService,
    IOptions<AppConfig> appConfig,
    ILogger<UnconfirmedTransactionsMonitor> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
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

                using var readSession = store.GetSession();
                var datas = await readSession
                    .LoadAsync<TransactionHexData>(missingTxs.Select(TransactionHexData.GetId), cancellationToken);

                foreach (var data in datas)
                {
                    if (data.Value is null)
                        continue;

                    try
                    {
                        var result = await broadcastService.Broadcast(data.Value.Hex);
                        if (result.Success)
                        {
                            _logger.LogDebug("Transaction re-broadcasted: {TxId}: {@Attempts}", data.Value.TxId, result.Attempts);
                            continue;
                        }

                        _logger.LogDebug("Failed re-broadcast: {TxId}: {Message}", data.Value.TxId, result.Message);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogDebug("Failed re-broadcast: {TxId}: {Error}", data.Value.TxId, exception.Message);
                    }
                }
            }

            _logger.LogDebug("Processed {Count} txs, {Remain} more", txIds.Count, txs.Count - processedTxs);

            if (blocksToSync.Any())
            {
                _logger.LogDebug("Blocks to sync: {@BlocksToSync}", blocksToSync);

                foreach (var hash in blocksToSync)
                {
                    blockMessageBus.Post(new BlockMessage(hash, TxObservationSource.Node));
                }
            }

            if (processedTxs >= txs.Count)
                break;
        }

        if (totalMissingTxs > 0)
            _logger.LogDebug("Total transactions was not in mempool {Count}", totalMissingTxs);

        _logger.LogDebug("Finished");
    }
}
