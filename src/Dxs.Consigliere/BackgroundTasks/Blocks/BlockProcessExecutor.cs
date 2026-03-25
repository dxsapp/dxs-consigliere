using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Rpc.Services;
using Dxs.Common.Extensions;
using Dxs.Common.Journal;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Notifications;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

using MediatR;

using Raven.Client.Documents;
using Raven.Client.Documents.Session;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks.Blocks;

public class BlockProcessExecutor(
    IRpcClient rpcClient,
    IServiceProvider serviceProvider,
    IPublisher publisher,
    IDocumentStore documentStore,
    IObservationJournalAppender<ObservationJournalEntry<BlockObservation>> blockObservationJournal,
    IOptions<ConsigliereSourcesConfig> sourcesConfig,
    IOptions<AppConfig> appConfig,
    IExternalChainProviderCatalog providerCatalog,
    ILogger<BlockProcessExecutor> logger
)
{
    public static readonly TimeSpan BlockProcessDelayStep = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxBlockProcessDelay = TimeSpan.FromMinutes(5);

    public async Task ExecuteAsync(string blockHash, CancellationToken cancellationToken)
    {
        using var _ = logger.BeginScope("{BlockHash}", blockHash);
        using var session = documentStore.GetSession();

        var blockProcessCtx = await session.LoadAsync<BlockProcessContext>(blockHash, cancellationToken);

        DateTime? utcNow = null;

        try
        {
            logger.LogDebug("Starting block process");

            await HandleBlock(blockProcessCtx, session, cancellationToken);
            utcNow = DateTime.UtcNow;

            blockProcessCtx.NextProcessAt = null;
            blockProcessCtx.ErrorsCount = 0;
            blockProcessCtx.Finish -= 1;

            var message = new BlockProcessed(blockProcessCtx.Height, blockProcessCtx.Id);

            await publisher.Publish(message);
        }
        catch (Exception exception)
        {
            utcNow = DateTime.UtcNow;
            blockProcessCtx.ErrorsCount += 1;

            var delayInSeconds = Math.Min(
                BlockProcessDelayStep.TotalSeconds * blockProcessCtx.ErrorsCount,
                MaxBlockProcessDelay.TotalSeconds
            );

            blockProcessCtx.NextProcessAt = utcNow.Value.AddSeconds(delayInSeconds);
            blockProcessCtx.Messages.Add(exception.Message);

            logger.LogError(exception, "Failed to process block: {Hash}", blockHash);
        }
        finally
        {
            logger.LogDebug("Finished block process");
            blockProcessCtx.Scheduled = false;
            blockProcessCtx.LastProcessAt = utcNow!.Value;

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task HandleBlock(
        BlockProcessContext context,
        IAsyncDocumentSession session,
        CancellationToken cancellationToken
    )
    {
        var blockHeaderResponse = await rpcClient.GetBlockHeader(context.Id);
        var blockHeader = blockHeaderResponse.Result;

        if (blockHeaderResponse.Error?.Message == "Block not found" || blockHeader?.Confirmations == -1)
        {
            await HandleBlockNotFound(context, session, cancellationToken);

            return;
        }

        if (blockHeaderResponse.Error != null)
            throw new Exception(blockHeaderResponse.Error.Message);

        if (blockHeader == null)
            throw new Exception("Rpc returned success response without result");

        logger.LogDebug("Starting block handling: {@Context}; {@BlockHeader}", context, blockHeader);

        context.Height = blockHeader.Height;
        context.Timestamp = blockHeader.Time;

        await HandleValidBlock(context, cancellationToken);

        context.TransactionsCount = blockHeader.TransactionsCount;
    }

    private async Task HandleValidBlock(BlockProcessContext context, CancellationToken cancellationToken)
    {
        using var _ = serviceProvider.GetScopedServices(
            out JungleBusBlockchainDataProvider jungleBusBlockchainDataProvider,
            out NodeBlockchainDataProvider nodeBlockchainDataProvider
        );

        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.BlockBackfill,
            sourcesConfig.Value,
            appConfig.Value,
            providerCatalog
        );
        var selectedSource = SourceCapabilityRouting.SelectForAttempt(route, context.ErrorsCount);

        var dataProvider = string.Equals(selectedSource, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase)
            && jungleBusBlockchainDataProvider.Enabled
            ? (IBlockDataProvider)jungleBusBlockchainDataProvider
            : nodeBlockchainDataProvider;

        using var timesCts = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(timesCts.Token, cancellationToken);

        logger.LogDebug(
            "Selected source `{Source}` for capability `{Capability}`; primary `{Primary}`, fallbacks {Fallbacks}, verification `{Verification}`",
            selectedSource,
            route.Capability,
            route.PrimarySource,
            route.FallbackSources,
            route.VerificationSource
        );

        await dataProvider.ProcessBlock(context, combined.Token);

        logger.LogDebug("Handling block {@Context} finished", context);
    }

    private async Task HandleBlockNotFound(
        BlockProcessContext context,
        IAsyncDocumentSession session,
        CancellationToken cancellationToken
    )
    {
        context.NotFound++;

        if (context.NotFound < 20)
            throw new Exception("Unable to get block from the chain");

        logger.LogWarning("Block {@BlockId} was removed; {@Context}", context.Id, context);

        context.NextProcessAt = null;
        context.Orphaned = true;
        context.Messages.Add("Block wasn't found in blockchain");

        var observation = new BlockObservation(
            BlockObservationEventType.Disconnected,
            TxObservationSource.Node,
            context.Id,
            DateTimeOffset.UtcNow,
            "orphaned"
        );
        var request = new ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>>(
            new ObservationJournalEntry<BlockObservation>(observation),
            new DedupeFingerprint($"{TxObservationSource.Node}|{BlockObservationEventType.Disconnected}|{context.Id}")
        );
        await blockObservationJournal.AppendAsync(request, cancellationToken);

        var transactions = await session.Query<MetaTransaction>()
            .Where(x => x.Block == context.Id)
            .ToListAsync(token: cancellationToken);

        foreach (var transaction in transactions)
        {
            transaction.Block = null;
            transaction.Height = 0;
            transaction.Index = 0;
        }
    }
}
