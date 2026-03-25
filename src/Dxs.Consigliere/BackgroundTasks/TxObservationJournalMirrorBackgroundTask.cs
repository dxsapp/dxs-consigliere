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
    IObservationJournalAppender<ObservationJournalEntry<TxObservation>> observationJournal,
    IRawTransactionPayloadStore rawTransactionPayloadStore,
    IOptions<ConsigliereStorageConfig> storageConfig,
    ILogger<TxObservationJournalMirrorBackgroundTask> logger
) : IHostedService, IBackgroundTask, IDisposable
{
    private const string RavenPayloadProvider = "raven";

    private readonly CancellationTokenSource _cts = new();
    private IDisposable _txSubscription;
    private IAgent<TxMessage> _messageHandler;
    private int _unsupportedPayloadStoreLogged;

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
            if (!TryCreateObservation(message, out var observation))
                return;

            var payloadReference = await TryPersistPayloadAsync(message, _cts.Token);
            var entry = new ObservationJournalEntry<TxObservation>(observation, payloadReference);
            var request = new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
                entry,
                BuildFingerprint(observation)
            );

            await observationJournal.AppendAsync(request, _cts.Token);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to mirror tx message {TxId} from {Source}", message.TxId, message.Source);
        }
    }

    private async Task<RawTransactionPayloadReference> TryPersistPayloadAsync(TxMessage message, CancellationToken cancellationToken)
    {
        if (message.Transaction is null)
            return null;

        var payloads = storageConfig.Value.RawTransactionPayloads;
        if (!payloads.Enabled)
            return null;

        if (!string.Equals(payloads.Provider, RavenPayloadProvider, StringComparison.OrdinalIgnoreCase))
        {
            if (Interlocked.Exchange(ref _unsupportedPayloadStoreLogged, 1) == 0)
            {
                logger.LogWarning(
                    "Raw transaction payload provider `{Provider}` is configured but not implemented in vnext yet. Mirror-write will continue without payload persistence.",
                    payloads.Provider
                );
            }

            return null;
        }

        var compressionAlgorithm = payloads.Compression?.Enabled == true
            ? payloads.Compression.Algorithm
            : RawTransactionPayloadCompressionAlgorithm.None;

        return await rawTransactionPayloadStore.SaveAsync(
            message.Transaction.Id,
            message.Transaction.Hex,
            compressionAlgorithm,
            cancellationToken
        );
    }

    private static bool TryCreateObservation(TxMessage message, out TxObservation observation)
    {
        observation = message.MessageType switch
        {
            TxMessage.Type.AddedToMempool => new TxObservation(
                TxObservationEventType.SeenInMempool,
                message.Source,
                message.TxId,
                GetObservedAt(message.Timestamp)
            ),
            TxMessage.Type.FoundInBlock => new TxObservation(
                TxObservationEventType.SeenInBlock,
                message.Source,
                message.TxId,
                GetObservedAt(message.Timestamp),
                message.BlockHash,
                message.Height,
                message.Idx
            ),
            TxMessage.Type.RemoveTransaction when message.Reason != RemoveFromMempoolReason.IncludedInBlock => new TxObservation(
                TxObservationEventType.DroppedBySource,
                message.Source,
                message.TxId,
                null,
                message.BlockHash,
                null,
                null,
                message.Reason.ToString(),
                message.CollidedWithTransaction
            ),
            _ => null
        };

        return observation is not null;
    }

    private static DedupeFingerprint BuildFingerprint(TxObservation observation)
    {
        var value = observation.EventType switch
        {
            TxObservationEventType.SeenInMempool => $"{observation.Source}|{observation.EventType}|{observation.TxId}",
            TxObservationEventType.SeenInBlock => $"{observation.Source}|{observation.EventType}|{observation.TxId}|{observation.BlockHash}",
            TxObservationEventType.DroppedBySource => $"{observation.Source}|{observation.EventType}|{observation.TxId}",
            _ => $"{observation.Source}|{observation.EventType}|{observation.TxId}"
        };

        return new DedupeFingerprint(value);
    }

    private static DateTimeOffset? GetObservedAt(long timestamp)
        => timestamp > 0
            ? DateTimeOffset.FromUnixTimeSeconds(timestamp)
            : null;
}
