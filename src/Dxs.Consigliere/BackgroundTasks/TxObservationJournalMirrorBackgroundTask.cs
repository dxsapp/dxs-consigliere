using System.Threading;

using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Dataflow;
using Dxs.Common.Interfaces;
using Dxs.Common.Journal;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks;

public sealed class TxObservationJournalMirrorBackgroundTask(
    ITxMessageBus txMessageBus,
    TxObservationJournalWriter journalWriter,
    IOptions<AppConfig> appConfig,
    ILogger<TxObservationJournalMirrorBackgroundTask> logger
) : IHostedService, IBackgroundTask, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private IDisposable _txSubscription;
    private IAgent<TxMessage> _messageHandler;

    public string Name => nameof(TxObservationJournalMirrorBackgroundTask);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _messageHandler ??= Agent.Start<TxMessage>(HandleAsync, _cts);
        _txSubscription ??= txMessageBus.Subscribe(
            message => _messageHandler.Post(message),
            exception => logger.LogError(exception, "Failed to mirror tx message to observation journal")
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _txSubscription?.Dispose();
        _messageHandler?.Complete();
        _cts.Cancel();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _txSubscription?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task HandleAsync(TxMessage message)
    {
        try
        {
            if (VNextCutoverMode.IsJournalFirst(appConfig.Value.VNextRuntime.CutoverMode))
                return;

            await journalWriter.AppendAsync(message, _cts.Token);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to mirror tx message {TxId} from {Source}", message.TxId, message.Source);
        }
    }
}
