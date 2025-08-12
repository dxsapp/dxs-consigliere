using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Rpc.Models;
using Dxs.Bsv.Rpc.Services;
using Dxs.Bsv.Zmq;
using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;
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
    IOptions<AppConfig> appConfig,
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
        await zmqClient.Start(cancellationToken);
        await Start(cancellationToken);
    }

    private async Task Start(CancellationToken cancellationToken)
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

        if (_appConfig.ScanMempoolOnStart)
        {
            _logger.LogDebug("Starting mempool scanning");

            var txs = await bitcoindService.GetMempoolTransactions();

            foreach (var transaction in txs)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                txMessageBus.Post(TxMessage.AddedToMempool(transaction, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            }

            _logger.LogDebug("Scanned {Count} from mempool", txs.Count);
        }

        if (_appConfig.BlockCountToScanOnStart > 0)
        {
            var height = await rpcClient.GetBlockCount().EnsureSuccess();

            _logger.LogDebug("Start to read {Count} blocks from the top", _appConfig.BlockCountToScanOnStart);

            for (var i = 0; i < _appConfig.BlockCountToScanOnStart; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var blockHeight = height - _appConfig.BlockCountToScanOnStart + i;
                var hash = await rpcClient.GetBlockHash(blockHeight).EnsureSuccess();

                blockMessageBus.Post(new BlockMessage(hash));
            }
        }

        _logger.LogDebug("App successfully started");
    }
}