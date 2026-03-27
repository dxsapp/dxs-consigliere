using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Rpc.Models;
using Dxs.Bsv.Rpc.Services;
using Dxs.Bsv.Zmq;
using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.BackgroundTasks.Realtime;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks;

public class AppInitBackgroundTask(
    IZmqClient zmqClient,
    ITransactionFilter transactionFilter,
    IBitcoindService bitcoindService,
    IRpcClient rpcClient,
    ITxMessageBus txMessageBus,
    IBlockMessageBus blockMessageBus,
    IDocumentStore store,
    IOptions<ConsigliereSourcesConfig> sourcesConfig,
    IOptions<AppConfig> appConfig,
    IExternalChainProviderCatalog providerCatalog,
    ILogger<AppInitBackgroundTask> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{

    // ReSharper disable once NotAccessedField.Local
    private readonly ITransactionFilter _transactionFilter = transactionFilter;

    private readonly AppConfig _appConfig = appConfig.Value;
    private readonly ILogger _logger = logger;

    protected override TimeSpan Period => Timeout.InfiniteTimeSpan;
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromMinutes(10);

    public override string Name => nameof(AppInitBackgroundTask);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var bootstrapPlan = RealtimeBootstrapPlanner.Build(
            SourceCapabilityRouting.Resolve(
                ExternalChainCapability.RealtimeIngest,
                sourcesConfig.Value,
                _appConfig,
                providerCatalog),
            SourceCapabilityRouting.Resolve(
                ExternalChainCapability.BlockBackfill,
                sourcesConfig.Value,
                _appConfig,
                providerCatalog));

        if (bootstrapPlan.NodeZmqTopics != ZmqSubscriptionTopics.None)
        {
            _logger.LogInformation(
                "Starting node ZMQ bootstrap with topics {Topics}; realtime primary `{RealtimePrimary}`; block primary `{BlockPrimary}`",
                bootstrapPlan.NodeZmqTopics,
                bootstrapPlan.RealtimePrimarySource,
                bootstrapPlan.BlockBackfillPrimarySource);
            await zmqClient.Start(bootstrapPlan.NodeZmqTopics, cancellationToken);
        }
        else
        {
            _logger.LogInformation(
                "Skipping node ZMQ bootstrap because realtime primary is `{RealtimePrimary}` and block primary is `{BlockPrimary}`",
                bootstrapPlan.RealtimePrimarySource,
                bootstrapPlan.BlockBackfillPrimarySource);
        }

        await Start(bootstrapPlan, cancellationToken);
    }

    private async Task Start(RealtimeBootstrapPlan bootstrapPlan, CancellationToken cancellationToken)
    {
        using var session = store.GetSession();

        var blocks = await session
            .Query<BlockProcessContext>()
            .Where(x => x.Scheduled)
            .ToListAsync(token: cancellationToken);

        if (blocks.Any())
        {
            foreach (var block in blocks)
                block.Scheduled = false;

            await session.SaveChangesAsync(cancellationToken);
        }

        if (_appConfig.ScanMempoolOnStart && bootstrapPlan.ScanMempoolOnStart)
        {
            _logger.LogDebug("Starting mempool scanning");

            var txs = await bitcoindService.GetMempoolTransactions();

            foreach (var transaction in txs)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                txMessageBus.Post(TxMessage.AddedToMempool(
                    transaction,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    TxObservationSource.Node
                ));
            }

            _logger.LogDebug("Scanned {Count} from mempool", txs.Count);
        }
        else if (_appConfig.ScanMempoolOnStart)
        {
            _logger.LogInformation("Skipped node mempool scan on start because realtime ingest primary is `{Source}`", bootstrapPlan.RealtimePrimarySource);
        }

        if (_appConfig.BlockCountToScanOnStart > 0 && bootstrapPlan.ReplayRecentBlocksOnStart)
        {
            var height = await rpcClient.GetBlockCount().EnsureSuccess();

            _logger.LogDebug("Start to read {Count} blocks from the top", _appConfig.BlockCountToScanOnStart);

            for (var i = 0; i < _appConfig.BlockCountToScanOnStart; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var blockHeight = height - _appConfig.BlockCountToScanOnStart + i;
                var hash = await rpcClient.GetBlockHash(blockHeight).EnsureSuccess();

                blockMessageBus.Post(new BlockMessage(hash, TxObservationSource.Node));
            }
        }
        else if (_appConfig.BlockCountToScanOnStart > 0)
        {
            _logger.LogInformation("Skipped node block replay on start because block backfill primary is `{Source}`", bootstrapPlan.BlockBackfillPrimarySource);
        }

        _logger.LogDebug("App successfully started");
    }
}
