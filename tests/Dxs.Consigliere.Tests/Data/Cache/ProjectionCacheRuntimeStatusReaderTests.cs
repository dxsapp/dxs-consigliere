using Dxs.Common.Cache;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Journal;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Cache;

public class ProjectionCacheRuntimeStatusReaderTests : RavenTestDriver
{
    [Fact]
    public async Task GetSnapshotAsync_ReturnsEmptyLagSnapshotWhenCheckpointsAreMissing()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var invalidationTelemetry = new ProjectionCacheInvalidationTelemetry();
        var backfillService = new AddressHistoryEnvelopeBackfillService(store);
        var reader = new ProjectionCacheRuntimeStatusReader(store, invalidationTelemetry, backfillService);

        var snapshot = await reader.GetSnapshotAsync();

        Assert.Equal(0, snapshot.ProjectionLag.JournalTailSequence);
        Assert.Equal(0, snapshot.ProjectionLag.Address.CheckpointSequence);
        Assert.Equal(0, snapshot.ProjectionLag.Token.CheckpointSequence);
        Assert.Equal(0, snapshot.ProjectionLag.TxLifecycle.CheckpointSequence);
        Assert.Equal(0, snapshot.ProjectionLag.Address.Lag);
        Assert.Equal(0, snapshot.ProjectionLag.Token.Lag);
        Assert.Equal(0, snapshot.ProjectionLag.TxLifecycle.Lag);
    }

    [Fact]
    public async Task GetSnapshotAsync_ComposesLagInvalidationAndBackfillStatus()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        using (var session = store.OpenAsyncSession())
        {
            await session.StoreAsync(
                new ObservationJournalRecordDocument
                {
                    Id = ObservationJournalRecordDocument.GetId(new JournalSequence(12)),
                    Sequence = 12,
                    Fingerprint = "fp-12",
                    ObservationType = "tx",
                    ObservationJson = "{}",
                    AppendedAt = DateTimeOffset.UtcNow
                });
            await session.StoreAsync(
                new AddressProjectionCheckpointDocument
                {
                    Id = AddressProjectionCheckpointDocument.DocumentId,
                    LastSequence = 10,
                    LastFingerprint = "fp-10"
                });
            await session.StoreAsync(
                new TokenProjectionCheckpointDocument
                {
                    Id = TokenProjectionCheckpointDocument.DocumentId,
                    LastSequence = 11,
                    LastFingerprint = "fp-11"
                });
            await session.StoreAsync(
                new TxLifecycleProjectionCheckpointDocument
                {
                    Id = TxLifecycleProjectionCheckpointDocument.DocumentId,
                    LastSequence = 9,
                    LastFingerprint = "fp-9"
                });
            await session.StoreAsync(
                new AddressProjectionAppliedTransactionDocument
                {
                    Id = AddressProjectionAppliedTransactionDocument.GetId("tx-legacy"),
                    TxId = "tx-legacy"
                });
            await session.SaveChangesAsync();
        }

        var invalidationTelemetry = new ProjectionCacheInvalidationTelemetry();
        invalidationTelemetry.Record(
        [
            new ProjectionCacheTag("address-history:addr-1"),
            new ProjectionCacheTag("token-history:token-1")
        ]);

        var backfillService = new AddressHistoryEnvelopeBackfillService(store);
        var reader = new ProjectionCacheRuntimeStatusReader(store, invalidationTelemetry, backfillService);

        var snapshot = await reader.GetSnapshotAsync();

        Assert.Equal(12, snapshot.ProjectionLag.JournalTailSequence);
        Assert.Equal(2, snapshot.ProjectionLag.Address.Lag);
        Assert.Equal(1, snapshot.ProjectionLag.Token.Lag);
        Assert.Equal(3, snapshot.ProjectionLag.TxLifecycle.Lag);
        Assert.Equal(1, snapshot.Invalidation.Calls);
        Assert.Equal(2, snapshot.Invalidation.Tags);
        Assert.Equal(1, snapshot.HistoryEnvelopeBackfill.PendingCount);
    }
}
