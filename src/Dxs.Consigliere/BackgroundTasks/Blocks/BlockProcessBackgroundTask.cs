using Dxs.Bsv.BitcoinMonitor;
using Dxs.Common.BackgroundTasks;
using Dxs.Common.Dataflow;
using Dxs.Common.Extensions;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Extensions;

using Microsoft.Extensions.Options;

using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks.Blocks;

public class BlockProcessBackgroundTask : PeriodicTask, IDisposable
{
    private readonly IDocumentStore _documentStore;
    private readonly BlockProcessExecutor _blockProcessExecutor;
    private readonly ILogger _logger;

    private readonly IAgent<string> _blockHandlerAgent;
    private CancellationToken _cancellationToken;
    private readonly IDisposable _sub;

    public BlockProcessBackgroundTask(
        IBlockMessageBus blockMessageBus,
        BlockProcessExecutor blockProcessExecutor,
        IDocumentStore documentStore,
        IOptions<AppConfig> appConfig,
        ILogger<BlockProcessBackgroundTask> logger
    ) : base(appConfig.Value.BackgroundTasks, logger)
    {
        _blockProcessExecutor = blockProcessExecutor;
        _documentStore = documentStore;
        _logger = logger;

        _blockHandlerAgent = Agent.Start<string>(blockHash => _blockProcessExecutor.ExecuteAsync(blockHash, _cancellationToken));
        _sub = blockMessageBus.SubscribeAsync(
            async x => await TryScheduleBlockProcess(x.BlockHash),
            exception => _logger.LogError(exception, "Error during attempt to schedule block processing")
        );
    }

    protected override TimeSpan Period => TimeSpan.FromSeconds(5);
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromMinutes(1);
    public override string Name => nameof(BlockProcessBackgroundTask);

    protected override Task RunAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return RetrieveBlocksToHandle(cancellationToken);
    }

    private async Task RetrieveBlocksToHandle(CancellationToken cancellationToken)
    {
        const int effectiveQueueSize = 5;

        using var session = _documentStore.GetSession();

        try
        {
            if (_blockHandlerAgent.MessagesInQueue >= effectiveQueueSize)
                return;

            var processContexts = await session
                .Query<BlockProcessContext>()
                .NoStale()
                .Where(x => x.NextProcessAt != null)
                .Where(x => x.NextProcessAt <= DateTime.UtcNow)
                .Where(x => !x.Scheduled)
                .OrderBy(x => x.NextProcessAt)
                .Take(effectiveQueueSize)
                .ToListAsync(cancellationToken);

            if (processContexts.Count == 0)
                return;

            _logger.LogDebug("Found {Count} blocks to handle", processContexts.Count);

            foreach (var processContext in processContexts)
            {
                processContext.Scheduled = true;
                _blockHandlerAgent.Post(processContext.Id);
            }

            await session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to put blocks to handler");
        }
    }

    private async Task TryScheduleBlockProcess(string blockHash)
    {
        using var session = _documentStore.GetSession();

        var ctx = await session.LoadAsync<BlockProcessContext>(blockHash);

        if (ctx == null)
        {
            _logger.LogDebug("Creating new block process context for block: {BlockHash}", blockHash);

            ctx = new BlockProcessContext
            {
                Id = blockHash
            };

            await session.StoreAsync(ctx);
        }

        if (ctx.NextProcessAt == null)
        {
            ctx.NextProcessAt = DateTime.UtcNow;
            ctx.Start += 1;

            await session.SaveChangesAsync();
        }
    }

    public void Dispose()
    {
        _sub?.Dispose();
    }
}
