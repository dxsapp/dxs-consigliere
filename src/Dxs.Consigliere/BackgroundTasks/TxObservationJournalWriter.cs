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

    /// <summary>
    /// Wave 2 S0: source-neutral append overload. Used by the P2P
    /// mempool observer (and any future non-TxMessage source) to push
    /// a pre-built <see cref="TxObservation"/> into the journal. The
    /// caller is responsible for setting <c>observation.Source</c> to
    /// one of the <see cref="TxObservationSource"/> constants.
    ///
    /// Builds the same <see cref="DedupeFingerprint"/> as the
    /// <see cref="AppendAsync(TxMessage, CancellationToken)"/> overload
    /// via the shared <see cref="BuildFingerprint"/> helper, so
    /// observations of the same (source, event, txid) from this path
    /// dedupe correctly against ones from the TxMessage path.
    /// </summary>
    /// <param name="observation">already-built observation (caller sets Source)</param>
    /// <param name="payload">raw-tx reference, or null when raw wasn't persisted</param>
    /// <param name="source">redundant with observation.Source — kept as a
    /// separate parameter so audit grep on call sites is trivial,
    /// e.g. <c>rg -n 'AppendAsync\(.*,.*,.*"p2p"'</c>; the overload
    /// asserts the two agree.</param>
    public async Task<bool> AppendAsync(
        TxObservation observation,
        RawTransactionPayloadReference payload,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (observation is null) return false;
        if (string.IsNullOrEmpty(source)) return false;
        if (!string.Equals(observation.Source, source, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "TxObservationJournalWriter.AppendAsync: observation.Source ({Observation}) does not match source parameter ({Source}); dropping.",
                observation.Source,
                source);
            return false;
        }

        var entry = new ObservationJournalEntry<TxObservation>(observation, payload);
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
