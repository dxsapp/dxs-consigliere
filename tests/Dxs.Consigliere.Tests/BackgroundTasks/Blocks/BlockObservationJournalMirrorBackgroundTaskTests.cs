using System.Collections.Concurrent;

using Dxs.Bsv.BitcoinMonitor.Impl;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Journal;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.BackgroundTasks.Blocks;

public class BlockObservationJournalMirrorBackgroundTaskTests
{
    [Fact]
    public async Task MirrorsConnectedBlocksFromBlockBus()
    {
        var blockMessageBus = new BlockMessageBus();
        var journal = new FakeObservationJournal(expectedCount: 1);
        var task = CreateTask(blockMessageBus, journal);

        await task.StartAsync(CancellationToken.None);

        blockMessageBus.Post(new BlockMessage("block-hash", TxObservationSource.Node));

        await journal.WaitForCountAsync(1);

        var request = Assert.Single(journal.Requests);
        Assert.Equal(BlockObservationEventType.Connected, request.Observation.Observation.EventType);
        Assert.Equal("block-hash", request.Observation.Observation.BlockHash);
        Assert.Equal(TxObservationSource.Node, request.Observation.Observation.Source);
        Assert.Equal($"node|{BlockObservationEventType.Connected}|block-hash", request.Fingerprint.Value);

        await task.StopAsync(CancellationToken.None);
        task.Dispose();
    }

    [Fact]
    public async Task SkipsMirrorWritesWhenJournalFirstModeIsEnabled()
    {
        var blockMessageBus = new BlockMessageBus();
        var journal = new FakeObservationJournal(expectedCount: 1);
        var task = CreateTask(blockMessageBus, journal, VNextCutoverMode.ShadowRead);

        await task.StartAsync(CancellationToken.None);

        blockMessageBus.Post(new BlockMessage("block-hash", TxObservationSource.Node));

        await Task.Delay(250);

        Assert.Empty(journal.Requests);

        await task.StopAsync(CancellationToken.None);
        task.Dispose();
    }

    [Fact]
    public async Task DisposeIsIdempotentAfterStop()
    {
        var blockMessageBus = new BlockMessageBus();
        var journal = new FakeObservationJournal(expectedCount: 1);
        var task = CreateTask(blockMessageBus, journal);

        await task.StartAsync(CancellationToken.None);
        await task.StopAsync(CancellationToken.None);

        task.Dispose();
        task.Dispose();
    }

    private static BlockObservationJournalMirrorBackgroundTask CreateTask(
        BlockMessageBus blockMessageBus,
        FakeObservationJournal journal,
        string cutoverMode = VNextCutoverMode.Legacy
    )
        => new(
            blockMessageBus,
            new BlockObservationJournalWriter(journal),
            Options.Create(new AppConfig
            {
                VNextRuntime = new VNextRuntimeConfig
                {
                    CutoverMode = cutoverMode
                }
            }),
            NullLogger<BlockObservationJournalMirrorBackgroundTask>.Instance
        );

    private sealed class FakeObservationJournal(int expectedCount)
        : IObservationJournalAppender<ObservationJournalEntry<BlockObservation>>
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>>> Requests { get; } = new();

        public ValueTask<ObservationJournalAppendResult> AppendAsync(
            ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>> request,
            CancellationToken cancellationToken = default
        )
        {
            Requests.Enqueue(request);

            if (Requests.Count >= expectedCount)
                _completion.TrySetResult();

            return ValueTask.FromResult(new ObservationJournalAppendResult(new JournalSequence(Requests.Count), false));
        }

        public async Task WaitForCountAsync(int count)
        {
            if (Requests.Count >= count)
                return;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _completion.Task.WaitAsync(cts.Token);
            Assert.True(Requests.Count >= count, $"Expected at least {count} requests but observed {Requests.Count}.");
        }
    }
}
