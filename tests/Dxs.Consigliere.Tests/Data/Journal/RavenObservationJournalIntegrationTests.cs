using Dxs.Common.Journal;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Journal;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Journal;

public class RavenObservationJournalIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task AppendAsync_AllocatesMonotonicSequenceAndReplaysInOrder()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = new RavenObservationJournal<TestObservation>(store);

        var first = await sut.AppendAsync(
            new ObservationJournalAppendRequest<ObservationJournalEntry<TestObservation>>(
                new ObservationJournalEntry<TestObservation>(
                    new TestObservation("tx_seen_by_source", "tx-1"),
                    new RawTransactionPayloadReference("tx/payload/tx-1", "tx-1", RawTransactionPayloadCompressionAlgorithm.None)
                ),
                new DedupeFingerprint("node|tx_seen_by_source|tx-1")
            )
        );

        var second = await sut.AppendAsync(
            new ObservationJournalAppendRequest<ObservationJournalEntry<TestObservation>>(
                new ObservationJournalEntry<TestObservation>(
                    new TestObservation("tx_seen_in_block", "tx-2")
                ),
                new DedupeFingerprint("node|tx_seen_in_block|tx-2|block-9"),
                first.Sequence
            )
        );

        var replay = await sut.ReadAsync(JournalSequence.Empty, take: 10);

        Assert.False(first.IsDuplicate);
        Assert.False(second.IsDuplicate);
        Assert.Equal(1, first.Sequence.Value);
        Assert.Equal(2, second.Sequence.Value);
        Assert.Collection(
            replay,
            entry =>
            {
                Assert.Equal(1, entry.Sequence.Value);
                Assert.Equal("tx_seen_by_source", entry.Observation.EventType);
                Assert.NotNull(entry.PayloadReference);
                Assert.Equal("tx/payload/tx-1", entry.PayloadReference.DocumentId);
            },
            entry =>
            {
                Assert.Equal(2, entry.Sequence.Value);
                Assert.Equal("tx_seen_in_block", entry.Observation.EventType);
                Assert.Null(entry.PayloadReference);
            }
        );
    }

    [Fact]
    public async Task AppendAsync_DedupesByFingerprint()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = new RavenObservationJournal<TestObservation>(store);

        var request = new ObservationJournalAppendRequest<ObservationJournalEntry<TestObservation>>(
            new ObservationJournalEntry<TestObservation>(new TestObservation("tx_seen_in_mempool", "tx-3")),
            new DedupeFingerprint("node|tx_seen_in_mempool|tx-3")
        );

        var first = await sut.AppendAsync(request);
        var duplicate = await sut.AppendAsync(request);

        Assert.False(first.IsDuplicate);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(first.Sequence, duplicate.Sequence);

        using var session = store.OpenSession();
        Assert.Equal(1, session.Query<ObservationJournalRecordDocument>().Count());
        Assert.Equal(1, session.Query<ObservationJournalFingerprintDocument>().Count());
    }

    private sealed record TestObservation(string EventType, string TxId);
}
