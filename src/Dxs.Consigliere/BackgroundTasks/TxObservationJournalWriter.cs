using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks;

public sealed class TxObservationJournalWriter(
    IObservationJournalAppender<ObservationJournalEntry<TxObservation>> observationJournal,
    IRawTransactionPayloadStore rawTransactionPayloadStore,
    IOptions<ConsigliereStorageConfig> storageConfig,
    ILogger<TxObservationJournalWriter> logger
)
{
    private const string RavenPayloadProvider = "raven";
    private int _unsupportedPayloadStoreLogged;

    public async Task<bool> AppendAsync(TxMessage message, CancellationToken cancellationToken = default)
    {
        if (!TryCreateObservation(message, out var observation))
            return false;

        var payloadReference = await TryPersistPayloadAsync(message, cancellationToken);
        var entry = new ObservationJournalEntry<TxObservation>(observation, payloadReference);
        var request = new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
            entry,
            BuildFingerprint(observation)
        );

        await observationJournal.AppendAsync(request, cancellationToken);
        return true;
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
                    "Raw transaction payload provider `{Provider}` is configured but not implemented in vnext yet. Journal writes will continue without payload persistence.",
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

    internal static bool TryCreateObservation(TxMessage message, out TxObservation observation)
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

    internal static DedupeFingerprint BuildFingerprint(TxObservation observation)
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
