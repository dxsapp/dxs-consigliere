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
    IFilteredTransactionMessageBus filteredTransactionMessageBus,
    TxObservationJournalWriter journalWriter,
    IOptions<AppConfig> appConfig,
    ILogger<TxObservationJournalMirrorBackgroundTask> logger
) : IHostedService, IBackgroundTask, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private IDisposable _txSubscription;
    private IAgent<FilteredTransactionMessage> _messageHandler;

    public string Name => nameof(TxObservationJournalMirrorBackgroundTask);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _messageHandler ??= Agent.Start<FilteredTransactionMessage>(HandleAsync, _cts);
        _txSubscription ??= filteredTransactionMessageBus.Subscribe(
            message => _messageHandler.Post(message),
            exception => logger.LogError(exception, "Failed to mirror filtered tx message to observation journal")
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

    private async Task HandleAsync(FilteredTransactionMessage message)
    {
        try
        {
            if (VNextCutoverMode.IsJournalFirst(appConfig.Value.VNextRuntime.CutoverMode))
                return;

            if (string.IsNullOrWhiteSpace(message.SourceMessage.TxId))
            {
                logger.LogWarning("Skipping filtered tx journal mirror write because source observation metadata is missing");
                return;
            }

            await journalWriter.AppendAsync(message.SourceMessage, _cts.Token);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to mirror filtered tx message {TxId} from {Source}", message.SourceMessage.TxId, message.SourceMessage.Source);
        }
    }
}
