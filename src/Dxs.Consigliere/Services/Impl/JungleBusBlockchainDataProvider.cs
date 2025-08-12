using System.Reactive.Disposables;
using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Common.Extensions;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Extensions;
using Dxs.Infrastructure.JungleBus;
using Dxs.Infrastructure.JungleBus.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustMargin.Common.Extensions;

namespace Dxs.Consigliere.Services.Impl;

public class JungleBusBlockchainDataProvider(
    IServiceProvider serviceProvider,
    ITxMessageBus txMessageBus,
    IOptions<AppConfig> appConfig,
    ILogger<JungleBusBlockchainDataProvider> logger
): IBlockDataProvider
{
    private readonly JungleBusConfig _jungleBusConfig = appConfig.Value.JungleBus;
    private readonly ILogger _logger = logger;

    public Task<int> ProcessBlock(BlockProcessContext context, CancellationToken cancellationToken)
        => ProcessBlock(context.Height, cancellationToken);

    public Task<int> ProcessBlock(int height, CancellationToken cancellationToken)
        => ProcessBlock(height, _jungleBusConfig.BlockSubscriptionId, cancellationToken);

    public async Task<int> ProcessBlock(int height, string subscriptionId, CancellationToken cancellationToken)
    {
        using var _ = serviceProvider.GetScopedService(out JungleBusWebsocketClient jungleBus);

        await jungleBus.StartSubscription(subscriptionId);
        
        return await ProcessBlock(height, jungleBus, cancellationToken);
    }

    public async Task<int> ProcessBlock(int height, JungleBusWebsocketClient jungleBus, CancellationToken cancellationToken)
    {
        using var compositeSub = new CompositeDisposable();

        _logger.BeginScope("JungleBus Crawl block: {Height}", height).AddToCompositeDisposable(compositeSub);

        var blockFinished = false;
        var txsInBlock = 0;
        var error = string.Empty;
        var txCount = 0;

        jungleBus
            .Block
            .Subscribe(x =>
                {
                    if (x.BlockHeight == height)
                    {

                        if (x.TransactionBase64 == null)
                        {
                            _logger.LogDebug("No body transaction: {Hash}", x.Id);
                        }
                        else
                        {
                            var transaction = Transaction.Parse(Convert.FromBase64String(x.TransactionBase64), Network.Mainnet);

                            txMessageBus.Post(TxMessage.FoundInBlock(
                                transaction,
                                x.BlockTime,
                                x.BlockHash,
                                x.BlockHeight,
                                x.BlockIndex
                            ));
                        }
                        
                        txCount++;
                        
                        if ((txCount + 1) % 10000 == 0 || txCount + 1 == txsInBlock)
                            _logger.LogDebug("JungleBus processed: {Count}", txCount);
                    }
                }
            )
            .AddToCompositeDisposable(compositeSub);

        jungleBus
            .ControlMessages
            .Subscribe(x =>
            {
                _logger.LogDebug("Control message: {@Message}", x);

                if (blockFinished || error.IsNotNullOrEmpty())
                    return;

                if (x.Block >= height)
                {
                    switch (x.Code)
                    {
                        case (int)PubControlMessageDto.StatusCode.BlockDone:
                            blockFinished = true;
                            txsInBlock = height == x.Block
                                ? x.TransactionCount
                                : -1;

                            return;
                        case (int)PubControlMessageDto.StatusCode.Error
                            or (int)PubControlMessageDto.StatusCode.Reorg
                            or (int)PubControlMessageDto.StatusCode.SubscriptionError:
                            error = x.Message;
                            break;
                    }

                }
                // else
                // {
                // _logger.LogError("Another block: {@Message}", x);
                // error = $"Another block started to crawl: {x.Block}; {x.Message}";
                // }
            })
            .AddToCompositeDisposable(compositeSub);

        _logger.LogDebug("Crawl start");

        jungleBus.SubscribeToControlMessages();
        jungleBus.CrawlBlock(height).AddToCompositeDisposable(compositeSub);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (error.IsNotNullOrEmpty() || !jungleBus.IsRunning)
                break;

            if (blockFinished && txsInBlock >= txCount)
                break;

            if (blockFinished && txsInBlock == -1)
                break;

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }

        _logger.LogDebug("Crawl finished");

        if (error.IsNotNullOrEmpty())
            throw new Exception(error);

        return txCount;
    }
}