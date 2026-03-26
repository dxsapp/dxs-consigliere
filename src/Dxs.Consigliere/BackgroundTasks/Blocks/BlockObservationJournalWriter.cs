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
}
