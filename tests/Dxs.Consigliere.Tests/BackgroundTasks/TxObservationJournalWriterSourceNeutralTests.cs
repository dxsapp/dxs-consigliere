using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.BackgroundTasks;

/// <summary>
/// Wave 2 S0 — unit tests for the new source-neutral
/// <see cref="TxObservationJournalWriter.AppendAsync(TxObservation,
/// RawTransactionPayloadReference, string, System.Threading.CancellationToken)"/>
/// overload. Runs without Raven by stubbing the appender + payload
/// store.
/// </summary>
public class TxObservationJournalWriterSourceNeutralTests
{
    private sealed class CapturingAppender : IObservationJournalAppender<ObservationJournalEntry<TxObservation>>
    {
        public readonly List<ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>> Requests = new();

        public ValueTask<ObservationJournalAppendResult> AppendAsync(
            ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(new ObservationJournalAppendResult(new JournalSequence(Requests.Count), false));
        }
    }

    private sealed class NullPayloadStore : IRawTransactionPayloadStore
    {
        public Task<RawTransactionPayloadReference> SaveAsync(
            string txId, string payloadHex,
            string compressionAlgorithm = RawTransactionPayloadCompressionAlgorithm.None,
            CancellationToken cancellationToken = default)
            => Task.FromResult<RawTransactionPayloadReference>(null);

        public Task<RawTransactionPayloadEnvelope> LoadByTxIdAsync(
            string txId, CancellationToken cancellationToken = default)
            => Task.FromResult<RawTransactionPayloadEnvelope>(null);

        public Task<RawTransactionPayloadEnvelope> LoadAsync(
            RawTransactionPayloadReference reference, CancellationToken cancellationToken = default)
            => Task.FromResult<RawTransactionPayloadEnvelope>(null);
    }

    private static TxObservationJournalWriter BuildWriter(CapturingAppender appender) => new(
        appender,
        new NullPayloadStore(),
        Options.Create(new ConsigliereStorageConfig()),
        NullLogger<TxObservationJournalWriter>.Instance);

    [Fact]
    public async Task AppendAsync_SourceNeutral_AppendsObservationWithCorrectFingerprint()
    {
        var appender = new CapturingAppender();
        var writer = BuildWriter(appender);
        var obs = new TxObservation(
            TxObservationEventType.SeenInMempool,
            TxObservationSource.P2p,
            "tx-abc",
            DateTimeOffset.FromUnixTimeSeconds(1_710_000_000));

        var ok = await writer.AppendAsync(obs, payload: null, TxObservationSource.P2p);

        Assert.True(ok);
        Assert.Single(appender.Requests);
        var captured = appender.Requests[0];
        Assert.Equal(obs, captured.Observation.Observation);
        Assert.Null(captured.Observation.PayloadReference);
        // Fingerprint matches the TxMessage path convention:
        // "{source}|{eventType}|{txid}"
        Assert.Equal($"{TxObservationSource.P2p}|{TxObservationEventType.SeenInMempool}|tx-abc",
            captured.Fingerprint.Value);
    }

    [Fact]
    public async Task AppendAsync_SourceNeutral_DropsWhenObservationSourceMismatchesParam()
    {
        var appender = new CapturingAppender();
        var writer = BuildWriter(appender);
        var obs = new TxObservation(
            TxObservationEventType.SeenInMempool,
            TxObservationSource.P2p,        // observation says p2p
            "tx-abc");

        // …but the caller passes "bitails" as the source param. The
        // overload rejects this to prevent silent source-tag drift.
        var ok = await writer.AppendAsync(obs, payload: null, TxObservationSource.Bitails);

        Assert.False(ok);
        Assert.Empty(appender.Requests);
    }

    [Fact]
    public async Task AppendAsync_SourceNeutral_DropsOnNullObservation()
    {
        var appender = new CapturingAppender();
        var writer = BuildWriter(appender);

        var ok = await writer.AppendAsync(observation: null, payload: null, TxObservationSource.P2p);

        Assert.False(ok);
        Assert.Empty(appender.Requests);
    }

    [Fact]
    public async Task AppendAsync_SourceNeutral_DropsOnEmptySource()
    {
        var appender = new CapturingAppender();
        var writer = BuildWriter(appender);
        var obs = new TxObservation(
            TxObservationEventType.SeenInMempool,
            "",                              // empty source on observation
            "tx-abc");

        var ok = await writer.AppendAsync(obs, payload: null, "");

        Assert.False(ok);
        Assert.Empty(appender.Requests);
    }

    [Fact]
    public async Task AppendAsync_SourceNeutral_AppendsWithPayloadReferenceWhenProvided()
    {
        var appender = new CapturingAppender();
        var writer = BuildWriter(appender);
        var obs = new TxObservation(
            TxObservationEventType.SeenInMempool,
            TxObservationSource.P2p,
            "tx-payload");
        var payload = new RawTransactionPayloadReference(
            "raw-tx-payloads/tx-payload",
            "tx-payload",
            RawTransactionPayloadCompressionAlgorithm.None);
        // ^ third arg is compression algorithm name (string); matches
        // RawTransactionPayloadCompressionAlgorithm.None static field.

        var ok = await writer.AppendAsync(obs, payload, TxObservationSource.P2p);

        Assert.True(ok);
        Assert.Single(appender.Requests);
        Assert.Equal(payload, appender.Requests[0].Observation.PayloadReference);
    }

    private sealed class DuplicateAppender : IObservationJournalAppender<ObservationJournalEntry<TxObservation>>
    {
        public int CallCount;
        public ValueTask<ObservationJournalAppendResult> AppendAsync(
            ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            // Simulate the journal recognising the entry as a duplicate.
            return ValueTask.FromResult(new ObservationJournalAppendResult(new JournalSequence(CallCount), isDuplicate: true));
        }
    }

    [Fact]
    public async Task AppendAsync_SourceNeutral_ReturnsFalseWhenJournalReportsDuplicate()
    {
        // Audit W2 S0-A1 H1: the overload must propagate
        // ObservationJournalAppendResult.IsDuplicate to the caller so
        // dedupe semantics flow through. A duplicate write is a
        // successful no-op at the journal but counts as "no new
        // observation recorded" from the caller's perspective.
        var appender = new DuplicateAppender();
        var writer = new TxObservationJournalWriter(
            appender,
            new NullPayloadStore(),
            Options.Create(new ConsigliereStorageConfig()),
            NullLogger<TxObservationJournalWriter>.Instance);

        var obs = new TxObservation(
            TxObservationEventType.SeenInMempool,
            TxObservationSource.P2p,
            "tx-dup");

        var ok = await writer.AppendAsync(obs, payload: null, TxObservationSource.P2p);

        Assert.False(ok);
        Assert.Equal(1, appender.CallCount);
    }
}
