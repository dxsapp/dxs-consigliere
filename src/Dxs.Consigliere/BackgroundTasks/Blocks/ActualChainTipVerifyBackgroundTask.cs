using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Rpc.Models;
using Dxs.Bsv.Rpc.Services;
using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks.Blocks;

public class ActualChainTipVerifyBackgroundTask(
    IDocumentStore store,
    IRpcClient rpcClient,
    IBlockMessageBus blockMessageBus,
    IOptions<AppConfig> appConfig,
    ILogger<ActualChainTipVerifyBackgroundTask> logger
): PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    private readonly ILogger _logger = logger;

    protected override TimeSpan Period => TimeSpan.FromSeconds(15);
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromMinutes(1);
    public override string Name => nameof(ActualChainTipVerifyBackgroundTask);

    protected override Task RunAsync(CancellationToken cancellationToken)
        => VerifyChainTip(cancellationToken);

    private async Task VerifyChainTip(CancellationToken cancellationToken)
    {
        using var session = store.GetSession();
        var hasNonSyncedBlocks = await session.Query<BlockProcessContext>()
            .Where(x => x.Height == 0)
            .Where(x => x.NextProcessAt != null)
            .AnyAsync(cancellationToken);

        if (hasNonSyncedBlocks)
        {
            _logger.LogWarning("Not all blocks synchronized");
            return;
        }

        var hasBlocks = await session
            .Query<BlockProcessContext>()
            .Where(x => x.Height != 0)
            .AnyAsync(cancellationToken);

        if (!hasBlocks)
        {
            _logger.LogWarning("No blocks at all");
            return;
        }

        var top = await rpcClient.GetBlockCount().EnsureSuccess();
        var topHash = await rpcClient.GetBlockHash(top).EnsureSuccess();

        var topKnownBlock = await session
            .Query<BlockProcessContext>()
            .Where(x => x.Height != 0)
            .OrderByDescending(x => x.Height)
            .FirstAsync(cancellationToken);

        if (top == topKnownBlock.Height && topHash != topKnownBlock.Id)
        {
            _logger.LogError("Reorg detected: {@Context}", topKnownBlock);

            var query = session
                .Query<BlockProcessContext>()
                .Where(x => x.Height != 0)
                .OrderByDescending(x => x.Height);

            await using var stream = await session.Advanced.StreamAsync(query, cancellationToken);

            while (await stream.MoveNextAsync())
            {
                var block = stream.Current.Document;
                var hash = await rpcClient.GetBlockHash(block.Height).EnsureSuccess();

                if (hash == block.Id)
                    return;

                _logger.LogWarning("Found block mis height: {@Block}", block);

                blockMessageBus.Post(new BlockMessage(block.Id));
                blockMessageBus.Post(new BlockMessage(hash));
            }
        }
    }
}