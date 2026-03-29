using System.Collections.Concurrent;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Impl;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.BackgroundTasks;

public class TxObservationJournalMirrorBackgroundTaskTests
{
    [Fact]
    public async Task MirrorsMempoolAndBlockObservationsWithPayloadReferences()
    {
        var filteredBus = new FilteredTransactionMessageBus();
        var journal = new FakeObservationJournal(expectedCount: 2);
        var payloadStore = new FakeRawTransactionPayloadStore();
        var task = CreateTask(filteredBus, journal, payloadStore);

        await task.StartAsync(CancellationToken.None);

        var tx = Transaction.Parse(GetSampleTransactionHex(), Network.Mainnet);
        filteredBus.Post(new FilteredTransactionMessage(
            tx,
            [],
            TxMessage.AddedToMempool(tx, 1_710_000_000, TxObservationSource.Node)));
        filteredBus.Post(new FilteredTransactionMessage(
            tx,
            [],
            TxMessage.FoundInBlock(tx, 1_710_000_100, TxObservationSource.JungleBus, "block-hash", 123, 4)));

        await journal.WaitForCountAsync(2);

        var mempool = Assert.Single(journal.Requests, x => x.Observation.Observation.EventType == TxObservationEventType.SeenInMempool);
        Assert.Equal(TxObservationSource.Node, mempool.Observation.Observation.Source);
        Assert.Equal(tx.Id, mempool.Observation.Observation.TxId);
        Assert.Equal(tx.Id, mempool.Observation.PayloadReference.TxId);
        Assert.Equal($"node|{TxObservationEventType.SeenInMempool}|{tx.Id}", mempool.Fingerprint.Value);

        var block = Assert.Single(journal.Requests, x => x.Observation.Observation.EventType == TxObservationEventType.SeenInBlock);
        Assert.Equal(TxObservationSource.JungleBus, block.Observation.Observation.Source);
        Assert.Equal("block-hash", block.Observation.Observation.BlockHash);
        Assert.Equal(123, block.Observation.Observation.BlockHeight);
        Assert.Equal(4, block.Observation.Observation.TransactionIndex);
        Assert.Equal($"junglebus|{TxObservationEventType.SeenInBlock}|{tx.Id}|block-hash", block.Fingerprint.Value);

        Assert.Equal([tx.Id], payloadStore.SavedTxIds);

        await task.StopAsync(CancellationToken.None);
        task.Dispose();
    }

    [Fact]
    public async Task SkipsMirrorWritesWhenJournalFirstModeIsEnabled()
    {
        var filteredBus = new FilteredTransactionMessageBus();
        var journal = new FakeObservationJournal(expectedCount: 1);
        var task = CreateTask(
            filteredBus,
            journal,
            new FakeRawTransactionPayloadStore(),
            cutoverMode: VNextCutoverMode.ShadowRead
        );

        await task.StartAsync(CancellationToken.None);

        var tx = Transaction.Parse(GetSampleTransactionHex(), Network.Mainnet);
        filteredBus.Post(new FilteredTransactionMessage(
            tx,
            [],
            TxMessage.AddedToMempool(tx, 1_710_000_000, TxObservationSource.Node)));

        await Task.Delay(250);

        Assert.Empty(journal.Requests);

        await task.StopAsync(CancellationToken.None);
        task.Dispose();
    }

    private static string GetSampleTransactionHex()
        => "0100000001c6f4b6176d3f4d6c6d9e198ba89a4eb7a1b08e6a705cc8cf0f8f2f3e3bcedf1f000000006b4830450221009af2d63b8ef3ebf8c7a227327d8e1a89f5929087566bbb6d6f74a09a87e2375d022007f8cefa32f6d829bb3f8792dd11e5d8f1cb4e4f4f84f7a8d431fed0b8ff103a4121022b698a0f0a1f1fb43fb8f33c2d72cbe7f3f8d98ef1a304681140f64e5681970fffffffff02e8030000000000001976a91489abcdefabbaabbaabbaabbaabbaabbaabbaabba88ac0000000000000000066a040102030400000000";

    private static TxObservationJournalMirrorBackgroundTask CreateTask(
        FilteredTransactionMessageBus filteredBus,
        FakeObservationJournal journal,
        FakeRawTransactionPayloadStore payloadStore,
        string cutoverMode = VNextCutoverMode.Legacy
    )
        => new(
            filteredBus,
            new TxObservationJournalWriter(
                journal,
                payloadStore,
                Options.Create(new ConsigliereStorageConfig
                {
                    RawTransactionPayloads =
                    {
                        Enabled = true,
                        Provider = "raven"
                    }
                }),
                NullLogger<TxObservationJournalWriter>.Instance
            ),
            Options.Create(new AppConfig
            {
                VNextRuntime = new VNextRuntimeConfig
                {
                    CutoverMode = cutoverMode
                }
            }),
            NullLogger<TxObservationJournalMirrorBackgroundTask>.Instance
        );

    private sealed class FakeObservationJournal(int expectedCount)
        : IObservationJournalAppender<ObservationJournalEntry<TxObservation>>
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>> Requests { get; } = new();

        public ValueTask<ObservationJournalAppendResult> AppendAsync(
            ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> request,
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

    private sealed class FakeRawTransactionPayloadStore : IRawTransactionPayloadStore
    {
        private readonly ConcurrentDictionary<string, string> _savedTxIds = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> SavedTxIds => _savedTxIds.Keys.ToArray();

        public Task<RawTransactionPayloadReference> SaveAsync(
            string txId,
            string payloadHex,
            string compressionAlgorithm = RawTransactionPayloadCompressionAlgorithm.None,
            CancellationToken cancellationToken = default
        )
        {
            _savedTxIds.TryAdd(txId, payloadHex);

            return Task.FromResult(new RawTransactionPayloadReference($"raw-tx-payloads/{txId}", txId, compressionAlgorithm));
        }

        public Task<RawTransactionPayloadEnvelope> LoadByTxIdAsync(
            string txId,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<RawTransactionPayloadEnvelope> LoadAsync(
            RawTransactionPayloadReference reference,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();
    }
}
