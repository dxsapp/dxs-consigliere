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
            var projectionCache = await LoadProjectionCacheAsync(session, records, cancellationToken);

            foreach (var record in records)
            {
                await ApplyAsync(session, projectionCache, record, cancellationToken);
                checkpoint = checkpoint.AdvanceTo(record.Sequence, record.Fingerprint);
            }

            await StoreCheckpointAsync(session, checkpoint, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        return checkpoint;
    }

    private async Task ApplyAsync(
        IAsyncDocumentSession session,
        Dictionary<string, TxLifecycleProjectionDocument> projectionCache,
        StoredObservationJournalRecord record,
        CancellationToken cancellationToken
    )
    {
        if (record.IsObservationType<TxObservation>())
        {
            await ApplyTxObservationAsync(session, projectionCache, record, record.Deserialize<TxObservation>(), cancellationToken);
            return;
        }

        if (record.IsObservationType<BlockObservation>())
        {
            await ApplyBlockObservationAsync(session, projectionCache, record, record.Deserialize<BlockObservation>(), cancellationToken);
        }
    }

    private static async Task<Dictionary<string, TxLifecycleProjectionDocument>> LoadProjectionCacheAsync(
        IAsyncDocumentSession session,
        IReadOnlyList<StoredObservationJournalRecord> records,
        CancellationToken cancellationToken
    )
    {
        var txIds = records
            .Where(x => x.IsObservationType<TxObservation>())
            .Select(x => x.Deserialize<TxObservation>().TxId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(TxLifecycleProjectionDocument.GetId)
            .ToArray();

        if (txIds.Length == 0)
            return new Dictionary<string, TxLifecycleProjectionDocument>(StringComparer.OrdinalIgnoreCase);

        var loaded = await session.LoadAsync<TxLifecycleProjectionDocument>(txIds, cancellationToken);

        return loaded.Values
            .Where(x => x is not null)
            .ToDictionary(x => x.TxId, x => x, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task ApplyTxObservationAsync(
        IAsyncDocumentSession session,
        Dictionary<string, TxLifecycleProjectionDocument> projectionCache,
        StoredObservationJournalRecord record,
        TxObservation observation,
        CancellationToken cancellationToken
    )
    {
        if (!projectionCache.TryGetValue(observation.TxId, out var document))
        {
            document = new TxLifecycleProjectionDocument
            {
                Id = TxLifecycleProjectionDocument.GetId(observation.TxId),
                TxId = observation.TxId,
                RelevantToManagedScope = false,
                RelevanceTypes = []
            };

            projectionCache[observation.TxId] = document;
            await session.StoreAsync(document, document.Id, cancellationToken);
        }

        var observedAt = observation.ObservedAt ?? record.AppendedAt;

        document.Known = true;
        document.SeenBySources = Merge(document.SeenBySources, observation.Source);
        document.PayloadAvailable |= record.PayloadReference is not null;
        document.FirstSeenAt = document.FirstSeenAt is null || observedAt < document.FirstSeenAt
            ? observedAt
            : document.FirstSeenAt;
        document.LastObservedAt = document.LastObservedAt is null || observedAt > document.LastObservedAt
            ? observedAt
            : document.LastObservedAt;
        document.LastSequence = record.Sequence.Value;

        switch (observation.EventType)
        {
            case TxObservationEventType.SeenInMempool:
                if (string.Equals(document.LifecycleStatus, TxLifecycleStatus.Confirmed, StringComparison.Ordinal))
                    break;

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
                if (string.Equals(document.LifecycleStatus, TxLifecycleStatus.Confirmed, StringComparison.Ordinal))
                    break;

                document.LifecycleStatus = TxLifecycleStatus.Dropped;
                document.Authoritative = false;
                document.SeenInMempool = false;
                document.BlockHash = null;
                document.BlockHeight = null;
                break;
        }
    }

    private static async Task ApplyBlockObservationAsync(
        IAsyncDocumentSession session,
        Dictionary<string, TxLifecycleProjectionDocument> projectionCache,
        StoredObservationJournalRecord record,
        BlockObservation observation,
        CancellationToken cancellationToken
    )
    {
        if (!string.Equals(observation.EventType, BlockObservationEventType.Disconnected, StringComparison.Ordinal))
            return;

        var affected = projectionCache.Values
            .Where(x => string.Equals(x.BlockHash, observation.BlockHash, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (affected.Count == 0)
        {
            affected = await session.Query<TxLifecycleProjectionDocument>()
                .Where(x => x.BlockHash == observation.BlockHash)
                .ToListAsync(token: cancellationToken);

            foreach (var document in affected)
                projectionCache[document.TxId] = document;
        }

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
