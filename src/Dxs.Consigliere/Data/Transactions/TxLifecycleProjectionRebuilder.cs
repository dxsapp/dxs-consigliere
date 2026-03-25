using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Data.Transactions;

public sealed class TxLifecycleProjectionRebuilder(
    IDocumentStore documentStore,
    RavenObservationJournalReader journalReader
)
{
    private const int DefaultPageSize = 512;

    public async Task<ProjectionCheckpoint> RebuildAsync(
        int take = DefaultPageSize,
        CancellationToken cancellationToken = default
    )
    {
        if (take <= 0)
            throw new ArgumentOutOfRangeException(nameof(take));

        var checkpoint = await LoadCheckpointAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var records = await journalReader.ReadAsync(checkpoint.Sequence, take, cancellationToken);
            if (records.Count == 0)
                return checkpoint;

            using var session = documentStore.GetSession();

            foreach (var record in records)
            {
                await ApplyAsync(session, record, cancellationToken);
                checkpoint = checkpoint.AdvanceTo(record.Sequence, record.Fingerprint);
            }

            await StoreCheckpointAsync(session, checkpoint, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        return checkpoint;
    }

    private async Task ApplyAsync(
        IAsyncDocumentSession session,
        StoredObservationJournalRecord record,
        CancellationToken cancellationToken
    )
    {
        if (record.IsObservationType<TxObservation>())
        {
            await ApplyTxObservationAsync(session, record, record.Deserialize<TxObservation>(), cancellationToken);
            return;
        }

        if (record.IsObservationType<BlockObservation>())
        {
            await ApplyBlockObservationAsync(session, record, record.Deserialize<BlockObservation>(), cancellationToken);
        }
    }

    private static async Task ApplyTxObservationAsync(
        IAsyncDocumentSession session,
        StoredObservationJournalRecord record,
        TxObservation observation,
        CancellationToken cancellationToken
    )
    {
        var document = await session.LoadAsync<TxLifecycleProjectionDocument>(
            TxLifecycleProjectionDocument.GetId(observation.TxId),
            cancellationToken
        ) ?? new TxLifecycleProjectionDocument
        {
            Id = TxLifecycleProjectionDocument.GetId(observation.TxId),
            TxId = observation.TxId,
            RelevantToManagedScope = false,
            RelevanceTypes = []
        };

        var observedAt = observation.ObservedAt ?? record.AppendedAt;

        document.Known = true;
        document.SeenBySources = Merge(document.SeenBySources, observation.Source);
        document.PayloadAvailable |= record.PayloadReference is not null;
        document.FirstSeenAt ??= observedAt;
        document.LastObservedAt = observedAt;
        document.LastSequence = record.Sequence.Value;

        switch (observation.EventType)
        {
            case TxObservationEventType.SeenInMempool:
                document.LifecycleStatus = TxLifecycleStatus.SeenInMempool;
                document.Authoritative = false;
                document.SeenInMempool = true;
                break;
            case TxObservationEventType.SeenInBlock:
                document.LifecycleStatus = TxLifecycleStatus.Confirmed;
                document.Authoritative = true;
                document.SeenInMempool = false;
                document.BlockHash = observation.BlockHash;
                document.BlockHeight = observation.BlockHeight;
                break;
            case TxObservationEventType.DroppedBySource:
                document.LifecycleStatus = TxLifecycleStatus.Dropped;
                document.Authoritative = false;
                document.SeenInMempool = false;
                document.BlockHash = null;
                document.BlockHeight = null;
                break;
        }

        await session.StoreAsync(document, document.Id, cancellationToken);
    }

    private static async Task ApplyBlockObservationAsync(
        IAsyncDocumentSession session,
        StoredObservationJournalRecord record,
        BlockObservation observation,
        CancellationToken cancellationToken
    )
    {
        if (!string.Equals(observation.EventType, BlockObservationEventType.Disconnected, StringComparison.Ordinal))
            return;

        var affected = await session.Query<TxLifecycleProjectionDocument>()
            .Where(x => x.BlockHash == observation.BlockHash)
            .ToListAsync(token: cancellationToken);

        foreach (var document in affected)
        {
            document.LifecycleStatus = TxLifecycleStatus.Reorged;
            document.Authoritative = false;
            document.SeenInMempool = null;
            document.BlockHash = null;
            document.BlockHeight = null;
            document.LastObservedAt = observation.ObservedAt ?? record.AppendedAt;
            document.LastSequence = record.Sequence.Value;
        }
    }

    private async Task<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken cancellationToken)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var checkpoint = await session.LoadAsync<TxLifecycleProjectionCheckpointDocument>(
            TxLifecycleProjectionCheckpointDocument.DocumentId,
            cancellationToken
        );

        return checkpoint is null
            ? new ProjectionCheckpoint(JournalSequence.Empty)
            : new ProjectionCheckpoint(
                new JournalSequence(checkpoint.LastSequence),
                string.IsNullOrWhiteSpace(checkpoint.LastFingerprint)
                    ? null
                    : new DedupeFingerprint(checkpoint.LastFingerprint)
            );
    }

    private static Task StoreCheckpointAsync(
        IAsyncDocumentSession session,
        ProjectionCheckpoint checkpoint,
        CancellationToken cancellationToken
    )
        => session.StoreAsync(
            new TxLifecycleProjectionCheckpointDocument
            {
                Id = TxLifecycleProjectionCheckpointDocument.DocumentId,
                LastSequence = checkpoint.Sequence.Value,
                LastFingerprint = checkpoint.LastAppliedFingerprint?.Value
            },
            TxLifecycleProjectionCheckpointDocument.DocumentId,
            cancellationToken
        );

    private static string[] Merge(IEnumerable<string> existing, string value)
        => existing
            .Append(value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
