#nullable enable
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Journal;

namespace Dxs.Consigliere.BackgroundTasks.Blocks;

public sealed class BlockObservationJournalWriter(
    IObservationJournalAppender<ObservationJournalEntry<BlockObservation>> observationJournal
)
{
    public Task AppendConnectedAsync(BlockMessage message, CancellationToken cancellationToken = default)
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

        return observationJournal.AppendAsync(request, cancellationToken).AsTask();
    }

    /// <summary>
    /// Wave 3 S0 — append a <see cref="BlockObservationEventType.Disconnected"/>
    /// entry for an orphaned block. Idempotent by dedupe fingerprint
    /// <c>block.disconnected:{blockHash}:{source}</c>; returns <c>true</c>
    /// only on a freshly-appended entry (false on duplicate). Same
    /// <c>IsDuplicate</c> propagation contract as the W2
    /// <c>TxObservationJournalWriter.AppendAsync(TxObservation, ..., source, ct)</c>
    /// overload (audit W2 S0-A1 H1).
    /// </summary>
    public async Task<bool> AppendDisconnectedAsync(
        string blockHash,
        string source,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(blockHash)) return false;
        if (string.IsNullOrEmpty(source)) return false;

        var observation = new BlockObservation(
            EventType: BlockObservationEventType.Disconnected,
            Source: source,
            BlockHash: blockHash,
            ObservedAt: DateTimeOffset.UtcNow,
            Reason: reason!
        );

        var request = new ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>>(
            new ObservationJournalEntry<BlockObservation>(observation),
            new DedupeFingerprint($"block.disconnected:{blockHash}:{source}")
        );

        var result = await observationJournal.AppendAsync(request, cancellationToken);
        return !result.IsDuplicate;
    }
}
