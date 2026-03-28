using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Common.Extensions;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;
using Dxs.Infrastructure.JungleBus;
using Dxs.Infrastructure.JungleBus.Dto;

using Microsoft.Extensions.Logging;

namespace Dxs.Consigliere.BackgroundTasks.Realtime;

public sealed class JungleBusRealtimeIngestRunner(
    ITxMessageBus messageBus,
    IServiceProvider serviceProvider,
    INetworkProvider networkProvider,
    IAdminProviderConfigService providerConfigService,
    ILogger<JungleBusRealtimeIngestRunner> logger
)
{
    private readonly ILogger _logger = logger;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var _ = serviceProvider.GetScopedService(out JungleBusWebsocketClient jungleBus);
        using var __ = _logger.BeginScope("JungleBusRealtimeIngest");

        var jungleBusSettings = await providerConfigService.GetEffectiveJungleBusAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(jungleBusSettings.MempoolSubscriptionId))
            throw new InvalidOperationException("JungleBus realtime ingest requires a mempool subscription id.");

        await jungleBus.StartSubscription(jungleBusSettings.MempoolSubscriptionId);

        var error = false;

        using var ___ = jungleBus.Mempool.Subscribe(x =>
        {
            if (x.TransactionBase64.IsNullOrEmpty())
                return;

            var transaction = Transaction.Parse(Convert.FromBase64String(x.TransactionBase64), networkProvider.Network);

            messageBus.Post(TxMessage.AddedToMempool(
                transaction,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                TxObservationSource.JungleBus
            ));
            _logger.LogDebug("Tx found in JungleBus mempool: {Id}", x.Id);
        });

        using var ____ = jungleBus.ControlMessages.Subscribe(x =>
        {
            _logger.LogDebug("{@Message}", x);
            if (x.Code
                is (int)PubControlMessageDto.StatusCode.Disconnected
                or (int)PubControlMessageDto.StatusCode.Unsubscribed
                or (int)PubControlMessageDto.StatusCode.SubscriptionError
                or (int)PubControlMessageDto.StatusCode.Error)
            {
                _logger.LogError("Restarting JungleBus realtime websocket");
                error = true;
            }
        });

        jungleBus.SubscribeToMempool();
        jungleBus.SubscribeToControlMessages();

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            if (error)
                throw new InvalidOperationException("JungleBus realtime websocket reported an error state.");
        }
    }
}
