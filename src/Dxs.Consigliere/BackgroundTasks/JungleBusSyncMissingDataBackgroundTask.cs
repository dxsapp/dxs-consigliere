using System.Collections.Concurrent;
using System.Diagnostics;
using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Common.BackgroundTasks;
using Dxs.Common.Extensions;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;
using Dxs.Infrastructure.JungleBus;
using Dxs.Infrastructure.JungleBus.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks;

public class JungleBusSyncMissingDataBackgroundTask(
    IDocumentStore documentStore,
    HttpClient httpClient,
    ITransactionStore transactionStore,
    IDocumentStore store,
    ITxMessageBus txMessageBus,
    ITransactionFilter transactionFilter,
    IServiceProvider serviceProvider,
    INetworkProvider networkProvider,
    IOptions<AppConfig> appConfig,
    ILogger<JungleBusSyncMissingDataBackgroundTask> logger
): PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    protected override TimeSpan Period => TimeSpan.FromSeconds(5);

    protected override TimeSpan WaitTimeOnError => TimeSpan.FromMinutes(5);

    public override string Name => nameof(JungleBusSyncMissingDataBackgroundTask);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        if (await CollectData(cancellationToken))
        {
            await PrepareMissingTransaction(cancellationToken);
            await FetchMissingTransactions(cancellationToken);
        }
    }

    private async Task<bool> CollectData(CancellationToken cancellationToken)
    {
        if (transactionFilter.QueueLength() > 100)
        {
            logger.LogDebug("Transaction filter jammed: {Count}", transactionFilter.QueueLength());

            return false;
        }

        using var session = store.GetNoCacheNoTrackingSession();

        var now = DateTime.UtcNow;
        var requestIds = await session
            .Query<SyncRequest>()
            .Where(x => !x.Finished)
            .Where(x => x.StartAt == null || x.StartAt <= now)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        logger.LogDebug("{Count} tokens to sync found", requestIds.Count);

        if (!requestIds.Any())
            return true;

        foreach (var requestId in requestIds)
        {
            try
            {
                await SyncBatch(requestId, cancellationToken);
            }
            catch (Exception ex)
            {
                using var session2 = store.GetSession();
                var request = await session2.LoadAsync<SyncRequest>(requestId, cancellationToken);

                logger.LogError(ex, "Failed to process sync request: {Id}", requestId);

                request.StartAt = DateTime.UtcNow.AddMinutes(2);
                await session2.SaveChangesAsync(cancellationToken);
            }
        }

        return false;
    }

    private async Task PrepareMissingTransaction(CancellationToken cancellationToken)
    {
        using var session = documentStore.GetSession();

        var query = session
            .Query<MetaTransaction>()
            .Where(x => x.MissingTransactions.Count > 0)
            .OrderBy(x => x.Height);

        var stream = await session.Advanced
            .StreamAsync(query, out var stats, cancellationToken);

        if (stats.TotalResults == 0)
            return;

        logger.LogDebug("Found {Count} transactions with missing data", stats.TotalResults);

        var batchSize = 1000;
        var batch = new List<MetaTransaction>();

        while (await stream.MoveNextAsync())
        {
            batch.Add(stream.Current.Document);

            if (batch.Count >= batchSize)
            {
                await StoreMissingTransactions(batch);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await StoreMissingTransactions(batch);
        }
    }

    private async Task FetchMissingTransactions(CancellationToken cancellationToken)
    {
        using var session = documentStore.GetSession();

        var requests = await session
            .Query<MissingTransaction>()
            .Take(10000)
            .ToListAsync(token: cancellationToken);

        logger.LogDebug("Found {Count} missing transactions", requests.Count);

        if (requests.Count == 0)
            return;

        const int batchSize = 1000;

        for (var i = 0; i < requests.Count; i += batchSize)
        {
            var sw = Stopwatch.StartNew();
            var bag = new ConcurrentBag<string>();
            var batch = requests.Skip(i).Take(batchSize).ToList();

            await Parallel.ForEachAsync(
                batch,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 10
                },
                async (request, _) =>
                {
                    if (await ProcessTransaction(request.TxId))
                    {
                        bag.Add(request.Id);
                    }
                }
            );

            using var session2 = documentStore.GetSession();

            foreach (var requestId in bag)
                session2.Delete(requestId);

            await session2.SaveChangesAsync(cancellationToken);

            sw.Stop();

            logger.LogDebug(
                "{@FetchMissingTransactions}",
                new
                {
                    BatchSize = batchSize,
                    ProcessTime = sw.Elapsed.TotalSeconds
                }
            );
        }
    }

    private async Task<bool> ProcessTransaction(string transactionId)
    {
        try
        {
            var transactionDto = await GetTransaction(transactionId);
            var txRaw = Convert.FromBase64String(transactionDto.TransactionBase64);
            var transaction = Transaction.Parse(txRaw, networkProvider.Network);

            await transactionStore.SaveTransaction(
                transaction,
                transactionDto.BlockTime,
                null,
                transactionDto.BlockHash,
                transactionDto.BlockHeight,
                transactionDto.BlockIndex
            );

            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process transaction: {TxId}", transactionId);

            return false;
        }
    }

    private async Task SyncBatch(string requestId, CancellationToken cancellationToken)
    {
        using var _ = logger.BeginScope("{RequestId}", requestId);
        using var __ = serviceProvider.GetScopedService(out JungleBusWebsocketClient jungleBus);
        using var session = store.GetSession();

        var request = await session.LoadAsync<SyncRequest>(requestId, cancellationToken);
        var txCount = 0;
        var stop = false;

        await jungleBus.StartSubscription(request.SubscriptionId);

        jungleBus
            .Block
            .Subscribe(x =>
                {
                    txCount++;

                    request.FromHeight = x.BlockHeight;

                    if (x.BlockHeight > request.ToHeight)
                    {
                        stop = true;
                    }

                    if (x.TransactionBase64 == null)
                    {
                        logger.LogDebug("No body transaction: {Hash}", x.Id);
                    }
                    else
                    {
                        var transaction = Transaction.Parse(Convert.FromBase64String(x.TransactionBase64), networkProvider.Network);

                        txMessageBus.Post(TxMessage.FoundInBlock(
                            transaction,
                            x.BlockTime,
                            x.BlockHash,
                            x.BlockHeight,
                            x.BlockIndex
                        ));
                    }

                    if ((txCount + 1) % 10000 == 0)
                    {
                        logger.LogDebug("JungleBus processed: {Count}", txCount);
                    }

                    if (transactionFilter.QueueLength() > 100000)
                    {
                        logger.LogDebug("Transaction filter jammed: {Count}", transactionFilter.QueueLength());

                        stop = true;
                    }
                },
                e =>
                {
                    logger.LogError(e, "JungleBus error: {Count}", txCount);
                },
                cancellationToken
            )
            ;

        jungleBus
            .ControlMessages
            .Subscribe(x =>
            {
                logger.LogDebug("Control message: {@Message}", x);
            });

        jungleBus.SubscribeToControlMessages();

        using var ___ = jungleBus.CrawlBlock(request.FromHeight);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (stop || !jungleBus.IsRunning)
            {
                logger.LogDebug("JungleBus stopped: {@Stop}", new { stop, running = jungleBus.IsRunning });

                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }

        if (request.FromHeight < request.ToHeight)
        {
            if (!stop)
            {
                if (!request.FailedBlocks.Add(request.FromHeight))
                {
                    request.FromHeight += 1;
                }
            }
        }
        else
        {
            request.Finished = request.FromHeight >= request.ToHeight;
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    private async Task<PubTransactionDto> GetTransaction(string txId)
    {
        var query = $"https://junglebus.gorillapool.io/v1/transaction/get/{txId}";

        return await httpClient.GetOrThrowAsync<PubTransactionDto>(query);
    }

    private async Task StoreMissingTransactions(List<MetaTransaction> metaTransactions)
    {
        var sw = Stopwatch.StartNew();

        using var session = documentStore.GetSession();

        var missingIds = metaTransactions.SelectMany(x => x.MissingTransactions).Distinct();
        var transactions = await session.LoadAsync<MetaTransaction>(missingIds);

        await Parallel.ForEachAsync(
            transactions,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 10
            },
            async (entry, _) =>
            {
                if (entry.Value == null)
                {
                    await documentStore.AddOrUpdateEntity(new MissingTransaction { TxId = entry.Key });
                }
            }
        );

        sw.Stop();

        logger.LogDebug(
            "{@ProcessedMissingTransactions}",
            new
            {
                transactions.Count,
                ProcessTime = sw.Elapsed,
            }
        );
    }
}