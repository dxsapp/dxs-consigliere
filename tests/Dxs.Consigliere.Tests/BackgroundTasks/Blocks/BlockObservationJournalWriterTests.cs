using System.Collections.Concurrent;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.Data.Journal;

namespace Dxs.Consigliere.Tests.BackgroundTasks.Blocks;

/// <summary>
/// Wave 3 S0 — pins the contract for
/// <see cref="BlockObservationJournalWriter.AppendDisconnectedAsync"/>:
/// dedupe fingerprint shape <c>block.disconnected:{blockHash}:{source}</c>,
/// <c>IsDuplicate</c> propagation (mirrors W2 S0-A1 H1).
/// </summary>
public class BlockObservationJournalWriterTests
{
    private sealed class CapturingAppender : IObservationJournalAppender<ObservationJournalEntry<BlockObservation>>
    {
        public readonly ConcurrentBag<ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>>> Requests = new();

        // Optional override: by fingerprint, return IsDuplicate=true.
        public readonly HashSet<string> DuplicateFingerprints = new();

        public ValueTask<ObservationJournalAppendResult> AppendAsync(
            ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>> request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var isDup = DuplicateFingerprints.Contains(request.Fingerprint.Value);
            return ValueTask.FromResult(
                new ObservationJournalAppendResult(new JournalSequence(Requests.Count), isDuplicate: isDup));
        }
    }

    [Fact]
    public async Task AppendDisconnected_FirstCall_ReturnsTrue()
    {
        var appender = new CapturingAppender();
        var writer = new BlockObservationJournalWriter(appender);

        var result = await writer.AppendDisconnectedAsync(
            blockHash: "abc123",
            source: BlockObservationSource.Reorg,
            reason: "fork:def456");

        Assert.True(result);
        var req = Assert.Single(appender.Requests);
        Assert.Equal(BlockObservationEventType.Disconnected, req.Observation.Observation.EventType);
        Assert.Equal(BlockObservationSource.Reorg, req.Observation.Observation.Source);
        Assert.Equal("abc123", req.Observation.Observation.BlockHash);
        Assert.Equal("fork:def456", req.Observation.Observation.Reason);
    }

    [Fact]
    public async Task AppendDisconnected_RepeatCall_SameFingerprint_ReturnsFalse()
    {
        var appender = new CapturingAppender();
        appender.DuplicateFingerprints.Add(
            $"block.disconnected:abc123:{BlockObservationSource.Reorg}");

        var writer = new BlockObservationJournalWriter(appender);

        var result = await writer.AppendDisconnectedAsync(
            "abc123", BlockObservationSource.Reorg);

        Assert.False(result);
    }

    [Fact]
    public async Task AppendDisconnected_EmptyBlockHash_ReturnsFalse()
    {
        var appender = new CapturingAppender();
        var writer = new BlockObservationJournalWriter(appender);

        Assert.False(await writer.AppendDisconnectedAsync(
            blockHash: "", source: BlockObservationSource.Reorg));
        Assert.Empty(appender.Requests);
    }

    [Fact]
    public async Task AppendDisconnected_EmptySource_ReturnsFalse()
    {
        var appender = new CapturingAppender();
        var writer = new BlockObservationJournalWriter(appender);

        Assert.False(await writer.AppendDisconnectedAsync(
            blockHash: "abc123", source: ""));
        Assert.Empty(appender.Requests);
    }

    [Fact]
    public async Task AppendDisconnected_FingerprintIncludesBlockHashAndSource()
    {
        var appender = new CapturingAppender();
        var writer = new BlockObservationJournalWriter(appender);

        await writer.AppendDisconnectedAsync("abc123", BlockObservationSource.Reorg);

        var req = Assert.Single(appender.Requests);
        Assert.Equal(
            $"block.disconnected:abc123:{BlockObservationSource.Reorg}",
            req.Fingerprint.Value);
    }

    [Fact]
    public async Task AppendDisconnected_NullReason_StoresNullOnObservation()
    {
        var appender = new CapturingAppender();
        var writer = new BlockObservationJournalWriter(appender);

        await writer.AppendDisconnectedAsync(
            blockHash: "abc123",
            source: BlockObservationSource.Reorg,
            reason: null);

        var req = Assert.Single(appender.Requests);
        Assert.Null(req.Observation.Observation.Reason);
    }

    [Fact]
    public async Task BlockObservationSource_Reorg_HasExpectedValue()
    {
        // Pin the contract value. A rename here is a breaking change
        // for downstream consumers (the rebuilder + W4 metrics).
        Assert.Equal("reorg", BlockObservationSource.Reorg);
        Assert.Equal("node", BlockObservationSource.Node);
        Assert.Equal("junglebus", BlockObservationSource.JungleBus);
    }
}
