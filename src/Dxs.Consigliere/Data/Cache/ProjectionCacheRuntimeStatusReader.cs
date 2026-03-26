#nullable enable

using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Journal;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Data.Cache;

public interface IProjectionCacheRuntimeStatusReader
{
    Task<ProjectionCacheRuntimeStatusSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

public sealed class ProjectionCacheRuntimeStatusReader(
    IDocumentStore documentStore,
    IProjectionCacheInvalidationTelemetry invalidationTelemetry,
    IAddressHistoryEnvelopeBackfillTelemetry backfillTelemetry
) : IProjectionCacheRuntimeStatusReader
{
    public async Task<ProjectionCacheRuntimeStatusSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var journalTail = await session.Query<ObservationJournalRecordDocument>()
            .OrderByDescending(x => x.Sequence)
            .Select(x => x.Sequence)
            .FirstOrDefaultAsync(token: cancellationToken);

        var addressCheckpointTask = session.LoadAsync<AddressProjectionCheckpointDocument>(AddressProjectionCheckpointDocument.DocumentId, cancellationToken);
        var tokenCheckpointTask = session.LoadAsync<TokenProjectionCheckpointDocument>(TokenProjectionCheckpointDocument.DocumentId, cancellationToken);
        var txCheckpointTask = session.LoadAsync<TxLifecycleProjectionCheckpointDocument>(TxLifecycleProjectionCheckpointDocument.DocumentId, cancellationToken);

        await Task.WhenAll(addressCheckpointTask, tokenCheckpointTask, txCheckpointTask);

        return new ProjectionCacheRuntimeStatusSnapshot(
            new ProjectionLagSnapshot(
                journalTail,
                BuildProjectionLag("address", journalTail, addressCheckpointTask.Result?.LastSequence ?? JournalSequence.Empty.Value),
                BuildProjectionLag("token", journalTail, tokenCheckpointTask.Result?.LastSequence ?? JournalSequence.Empty.Value),
                BuildProjectionLag("tx_lifecycle", journalTail, txCheckpointTask.Result?.LastSequence ?? JournalSequence.Empty.Value)),
            invalidationTelemetry.GetSnapshot(),
            backfillTelemetry.GetSnapshot());
    }

    private static ProjectionLagItemSnapshot BuildProjectionLag(string name, long journalTail, long checkpoint)
        => new(name, checkpoint, Math.Max(0, journalTail - checkpoint));
}

public sealed record ProjectionCacheRuntimeStatusSnapshot(
    ProjectionLagSnapshot ProjectionLag,
    ProjectionCacheInvalidationTelemetrySnapshot Invalidation,
    AddressHistoryEnvelopeBackfillTelemetrySnapshot HistoryEnvelopeBackfill
);

public sealed record ProjectionLagSnapshot(
    long JournalTailSequence,
    ProjectionLagItemSnapshot Address,
    ProjectionLagItemSnapshot Token,
    ProjectionLagItemSnapshot TxLifecycle
);

public sealed record ProjectionLagItemSnapshot(
    string Projection,
    long CheckpointSequence,
    long Lag
);
