using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Common.Extensions;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;
using Dxs.Infrastructure.JungleBus;
using Dxs.Infrastructure.JungleBus.Dto;

using Microsoft.Extensions.Logging;

using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks;

public class JungleBusSyncRequestProcessor(
    IDocumentStore store,
    ITxMessageBus txMessageBus,
    ITransactionFilter transactionFilter,
    IServiceProvider serviceProvider,
    INetworkProvider networkProvider,
    ILogger<JungleBusSyncRequestProcessor> logger
) : IJungleBusSyncRequestProcessor
{
    private readonly ILogger _logger = logger;

    public async Task<bool> ProcessAsync(CancellationToken cancellationToken)
    {
        if (transactionFilter.QueueLength() > 100)
        {
            _logger.LogDebug("Transaction filter jammed: {Count}", transactionFilter.QueueLength());

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

        _logger.LogDebug("{Count} tokens to sync found", requestIds.Count);

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

                _logger.LogError(ex, "Failed to process sync request: {Id}", requestId);

                request.StartAt = DateTime.UtcNow.AddMinutes(2);
                await session2.SaveChangesAsync(cancellationToken);
            }
        }

        return false;
    }

    private async Task SyncBatch(string requestId, CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScope("{RequestId}", requestId);
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
                        _logger.LogDebug("No body transaction: {Hash}", x.Id);
                    }
                    else
                    {
                        var transaction = Transaction.Parse(Convert.FromBase64String(x.TransactionBase64), networkProvider.Network);

                        txMessageBus.Post(TxMessage.FoundInBlock(
                            transaction,
                            x.BlockTime,
                            TxObservationSource.JungleBus,
                            x.BlockHash,
                            x.BlockHeight,
                            x.BlockIndex
                        ));
                    }

                    if ((txCount + 1) % 10000 == 0)
                    {
                        _logger.LogDebug("JungleBus processed: {Count}", txCount);
                    }

                    if (transactionFilter.QueueLength() > 100000)
                    {
                        _logger.LogDebug("Transaction filter jammed: {Count}", transactionFilter.QueueLength());

                        stop = true;
                    }
                },
                e =>
                {
                    _logger.LogError(e, "JungleBus error: {Count}", txCount);
                },
                cancellationToken
            )
            ;

        jungleBus
            .ControlMessages
            .Subscribe(x =>
            {
                _logger.LogDebug("Control message: {@Message}", x);
            });

        jungleBus.SubscribeToControlMessages();

        using var ___ = jungleBus.CrawlBlock(request.FromHeight);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (stop || !jungleBus.IsRunning)
            {
                _logger.LogDebug("JungleBus stopped: {@Stop}", new { stop, running = jungleBus.IsRunning });

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
}
