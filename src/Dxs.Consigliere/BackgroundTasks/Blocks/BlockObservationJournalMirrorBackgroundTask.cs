using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Dataflow;
using Dxs.Common.Interfaces;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Journal;

using Microsoft.Extensions.Hosting;

namespace Dxs.Consigliere.BackgroundTasks.Blocks;

public sealed class BlockObservationJournalMirrorBackgroundTask(
    IBlockMessageBus blockMessageBus,
    IObservationJournalAppender<ObservationJournalEntry<BlockObservation>> observationJournal,
    ILogger<BlockObservationJournalMirrorBackgroundTask> logger
) : IHostedService, IBackgroundTask, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private IDisposable _blockSubscription;
    private IAgent<BlockMessage> _messageHandler;

    public string Name => nameof(BlockObservationJournalMirrorBackgroundTask);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _messageHandler ??= Agent.Start<BlockMessage>(HandleAsync, _cts);
        _blockSubscription ??= blockMessageBus.Subscribe(
            message => _messageHandler.Post(message),
            exception => logger.LogError(exception, "Failed to mirror block message to observation journal")
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _blockSubscription?.Dispose();
        _messageHandler?.Complete();
        _cts.Cancel();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _blockSubscription?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task HandleAsync(BlockMessage message)
    {
        try
        {
            var observation = new BlockObservation(
                BlockObservationEventType.Connected,
                message.Source,
                message.BlockHash
            );
            var request = new ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>>(
                new ObservationJournalEntry<BlockObservation>(observation),
                new DedupeFingerprint($"{message.Source}|{BlockObservationEventType.Connected}|{message.BlockHash}")
            );

            await observationJournal.AppendAsync(request, _cts.Token);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to mirror block message {BlockHash} from {Source}", message.BlockHash, message.Source);
        }
    }
}
