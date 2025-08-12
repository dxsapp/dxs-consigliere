using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.Rpc.Services;
using Dxs.Common.BackgroundTasks;
using Dxs.Common.Dataflow;
using Dxs.Common.Extensions;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Notifications;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.BackgroundTasks.Blocks;

public class BlockProcessBackgroundTask: PeriodicTask, IDisposable
{
    public static readonly TimeSpan BlockProcessDelayStep = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxBlockProcessDelay = TimeSpan.FromMinutes(5);

    private readonly IRpcClient _rpcClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPublisher _publisher;
    private readonly IDocumentStore _documentStore;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger _logger;

    private readonly IAgent<string> _blockHandlerAgent;
    private CancellationToken _cancellationToken;
    private readonly IDisposable _sub;

    public BlockProcessBackgroundTask(
        IBlockMessageBus blockMessageBus,
        IRpcClient rpcClient,
        IPublisher publisher,
        IDocumentStore documentStore,
        IServiceProvider serviceProvider,
        IOptions<AppConfig> appConfig,
        IWebHostEnvironment webHostEnvironment,
        ILogger<BlockProcessBackgroundTask> logger
    ): base(appConfig.Value.BackgroundTasks, logger)
    {
        _rpcClient = rpcClient;
        _publisher = publisher;
        _documentStore = documentStore;
        _serviceProvider = serviceProvider;
        _logger = logger;

        _blockHandlerAgent = Agent.Start<string>(SafeHandleBlock);
        _sub = blockMessageBus.SubscribeAsync(
            async x => await TryScheduleBlockProcess(x.BlockHash),
            exception => _logger.LogError(exception, "Error during attempt to schedule block processing")
        );

        _webHostEnvironment = webHostEnvironment;
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

    private async Task SafeHandleBlock(string blockHash)
    {
        using var _ = _logger.BeginScope("{BlockHash}", blockHash);
        using var session = _documentStore.GetSession();

        var blockProcessCtx = await session.LoadAsync<BlockProcessContext>(blockHash, _cancellationToken);

        DateTime? utcNow = null;

        try
        {
            _logger.LogDebug("Starting block process");
            
            await HandleBlock(blockProcessCtx, session);
            utcNow = DateTime.UtcNow;

            blockProcessCtx.NextProcessAt = null;
            blockProcessCtx.ErrorsCount = 0;
            blockProcessCtx.Finish -= 1;

            var message = new BlockProcessed(blockProcessCtx.Height, blockProcessCtx.Id);
            await _publisher.Publish(message);
        }
        catch (Exception exception)
        {
            utcNow = DateTime.UtcNow;
            blockProcessCtx.ErrorsCount += 1;

            var delayInSeconds = Math.Min(BlockProcessDelayStep.TotalSeconds * blockProcessCtx.ErrorsCount, MaxBlockProcessDelay.TotalSeconds);

            blockProcessCtx.NextProcessAt = utcNow.Value.AddSeconds(delayInSeconds);
            blockProcessCtx.Messages.Add(exception.Message);

            _logger.LogError(exception, "Failed to process block: {Hash}", blockHash);
        }
        finally
        {
            _logger.LogDebug("Finished block process");
            blockProcessCtx.Scheduled = false;
            blockProcessCtx.LastProcessAt = utcNow!.Value;

            await session.SaveChangesAsync();
        }
    }

    private async Task HandleBlock(BlockProcessContext context, IAsyncDocumentSession session)
    {
        var blockHeaderResponse = await _rpcClient.GetBlockHeader(context.Id);
        var blockHeader = blockHeaderResponse.Result;

        if (blockHeaderResponse.Error?.Message == "Block not found" || blockHeader?.Confirmations == -1)
        {
            await HandleBlockNotFound(context, session);

            return;
        }

        if (blockHeaderResponse.Error != null)
            throw new Exception(blockHeaderResponse.Error.Message);

        if (blockHeader == null)
            throw new Exception("Rpc returned success response without result");

        _logger.LogDebug("Starting block handling: {@Context}; {@BlockHeader}", context, blockHeader);

        context.Height = blockHeader.Height;
        context.Timestamp = blockHeader.Time;

        await HandleValidBlock(context);

        context.TransactionsCount = blockHeader.TransactionsCount;
    }

    private async Task HandleValidBlock(BlockProcessContext context)
    {
        using var _ = _serviceProvider.GetScopedServices(
            out JungleBusBlockchainDataProvider jungleBusBlockchainDataProvider,
            out NodeBlockchainDataProvider nodeBlockchainDataProvider
        );
        var dataProvider = _webHostEnvironment.IsProduction() && context.ErrorsCount % 2 == 1
            ? jungleBusBlockchainDataProvider
            : (IBlockDataProvider)nodeBlockchainDataProvider;

        using var timesCts = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(timesCts.Token, _cancellationToken);

        await dataProvider.ProcessBlock(context, combined.Token);

        _logger.LogDebug("Handling block {@Context} finished", context);
    }

    private async Task HandleBlockNotFound(BlockProcessContext context, IAsyncDocumentSession session)
    {
        context.NotFound++;

        if (context.NotFound < 20)
            throw new Exception("Unable to get block from the chain");

        _logger.LogWarning("Block {@BlockId} was removed; {@Context}", context.Id, context);

        context.NextProcessAt = null;
        context.Orphaned = true;
        context.Messages.Add("Block wasn't found in blockchain");

        var transactions = await session.Query<MetaTransaction>()
            .Where(x => x.Block == context.Id)
            .ToListAsync(token: _cancellationToken);

        foreach (var transaction in transactions)
        {
            transaction.Block = null;
            transaction.Height = 0;
            transaction.Index = 0;
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