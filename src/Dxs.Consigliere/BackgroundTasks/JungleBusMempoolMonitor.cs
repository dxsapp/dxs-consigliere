using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Common.BackgroundTasks;
using Dxs.Common.Extensions;
using Dxs.Common.Time;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;
using Dxs.Infrastructure.JungleBus;
using Dxs.Infrastructure.JungleBus.Dto;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks;

public class JungleBusMempoolMonitor(
    ITxMessageBus messageBus,
    IServiceProvider serviceProvider,
    INetworkProvider networkProvider,
    IOptions<AppConfig> appConfig,
    ILogger<JungleBusMempoolMonitor> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    private readonly AppConfig _appConfig = appConfig.Value;
    private readonly ILogger _logger = logger;

    protected override TimeSpan Period => Timeout.InfiniteTimeSpan;
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromSeconds(10);

    public override string Name => nameof(JungleBusMempoolMonitor);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var _ = serviceProvider.GetScopedService(out JungleBusWebsocketClient jungleBus);
        using var ____ = _logger.BeginScope("JungleBusMempoolListener");

        await jungleBus.StartSubscription(_appConfig.JungleBus.MempoolSubscriptionId);

        var error = false;

        using var __ = jungleBus.Mempool.Subscribe(x =>
        {
            if (x.TransactionBase64.IsNullOrEmpty())
                return;

            var transaction = Transaction.Parse(Convert.FromBase64String(x.TransactionBase64), networkProvider.Network);

            messageBus.Post(TxMessage.AddedToMempool(transaction, DateTime.UtcNow.ToUnixSeconds()));
            _logger.LogDebug("Tx found in GorillaPool mempool: {Id}", x.Id);
        });

        using var ___ = jungleBus.ControlMessages.Subscribe(x =>
        {
            _logger.LogDebug("{@Message}", x);
            if (x.Code
                is (int)PubControlMessageDto.StatusCode.Disconnected
                or (int)PubControlMessageDto.StatusCode.Unsubscribed
                or (int)PubControlMessageDto.StatusCode.SubscriptionError
                or (int)PubControlMessageDto.StatusCode.Error)
            {
                _logger.LogError("Restarting JungleBus mempool websocket");

                error = true;
            }
        });

        jungleBus.SubscribeToMempool();
        jungleBus.SubscribeToControlMessages();

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            if (error)
                break;
        }

        _logger.LogDebug("JungleBusMempoolListener Exit");
    }
}
