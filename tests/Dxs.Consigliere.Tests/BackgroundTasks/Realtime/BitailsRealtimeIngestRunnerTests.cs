using System.Collections.Concurrent;
using System.Reactive.Subjects;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Impl;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Consigliere.BackgroundTasks.Realtime;
using Dxs.Consigliere.Services;
using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.Bitails.Dto;
using Dxs.Infrastructure.Bitails.Realtime;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Logging.Abstractions;

namespace Dxs.Consigliere.Tests.BackgroundTasks.Realtime;

public class BitailsRealtimeIngestRunnerTests
{
    private const string SampleTransactionHex = "0100000001c6f4b6176d3f4d6c6d9e198ba89a4eb7a1b08e6a705cc8cf0f8f2f3e3bcedf1f000000006b4830450221009af2d63b8ef3ebf8c7a227327d8e1a89f5929087566bbb6d6f74a09a87e2375d022007f8cefa32f6d829bb3f8792dd11e5d8f1cb4e4f4f84f7a8d431fed0b8ff103a4121022b698a0f0a1f1fb43fb8f33c2d72cbe7f3f8d98ef1a304681140f64e5681970fffffffff02e8030000000000001976a91489abcdefabbaabbaabbaabbaabbaabbaabbaabba88ac0000000000000000066a040102030400000000";

    [Fact]
    public async Task PostsBitailsNotificationsIntoExistingTxFilterPipelineOnlyOncePerTxId()
    {
        var transaction = Transaction.Parse(SampleTransactionHex, Network.Mainnet);
        var watchedAddress = transaction.Outputs[0].Address;
        var txMessageBus = new TxMessageBus();
        var filteredBus = new FilteredTransactionMessageBus();
        var store = new RecordingTransactionStore();
        var sink = new RecordingObservationSink();
        using var filter = new TransactionFilter(
            txMessageBus,
            filteredBus,
            store,
            sink,
            NullLogger<TransactionFilter>.Instance);
        filter.ManageUtxoSetForAddress(watchedAddress);

        var realtimeClient = new FakeBitailsRealtimeIngestClient();
        var runner = new BitailsRealtimeIngestRunner(
            realtimeClient,
            new FakeScopeProvider(watchedAddress.Value),
            new FakeRawTransactionFetchService(transaction.Id, Convert.FromHexString(SampleTransactionHex)),
            new FakeProviderSettingsAccessor(),
            new TestNetworkProvider(),
            txMessageBus,
            new FakeBlockMessageBus(),
            NullLogger<BitailsRealtimeIngestRunner>.Instance);

        using var cts = new CancellationTokenSource();
        var runTask = runner.RunAsync(cts.Token);

        await realtimeClient.WaitForConnectAsync();
        realtimeClient.Publish(new BitailsRealtimeEvent(BitailsRealtimeEventKind.TransactionAdded, $"lock-address-{watchedAddress.Value}", DateTimeOffset.UtcNow, transaction.Id));
        realtimeClient.Publish(new BitailsRealtimeEvent(BitailsRealtimeEventKind.TransactionAdded, $"spent-address-{watchedAddress.Value}", DateTimeOffset.UtcNow, transaction.Id));

        await store.WaitForCountAsync(1);
        await sink.WaitForCountAsync(1);
        await Task.Delay(150);

        Assert.Single(store.SavedTransactions);
        Assert.Equal(transaction.Id, store.SavedTransactions.Single());
        Assert.Equal(1, realtimeClient.ConnectCount);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await runTask);
    }

    [Fact]
    public async Task PostsBitailsRemoveAndBlockEventsWithoutRawFetch()
    {
        var txMessageBus = new TxMessageBus();
        var blockMessageBus = new FakeBlockMessageBus();
        var removedMessages = new ConcurrentQueue<TxMessage>();
        var removalCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var txSubscription = txMessageBus.Subscribe(message =>
        {
            if (message.MessageType == TxMessage.Type.RemoveTransaction)
            {
                removedMessages.Enqueue(message);
                removalCompletion.TrySetResult();
            }
        });
        using var blockSubscription = blockMessageBus.Subscribe(message => blockCompletion.TrySetResult());

        var realtimeClient = new FakeBitailsRealtimeIngestClient();
        var runner = new BitailsRealtimeIngestRunner(
            realtimeClient,
            new FakeScopeProvider("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa"),
            new FakeRawTransactionFetchService(null, null),
            new FakeProviderSettingsAccessor(),
            new TestNetworkProvider(),
            txMessageBus,
            blockMessageBus,
            NullLogger<BitailsRealtimeIngestRunner>.Instance);

        using var cts = new CancellationTokenSource();
        var runTask = runner.RunAsync(cts.Token);

        await realtimeClient.WaitForConnectAsync();
        realtimeClient.Publish(new BitailsRealtimeEvent(
            BitailsRealtimeEventKind.TransactionRemoved,
            BitailsRealtimeTopicCatalog.ZmqDiscardedFromMempoolTopic,
            DateTimeOffset.UtcNow,
            TxId: "89abcdefabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba",
            RemoveReason: "collision-in-block-tx",
            CollidedWithTransaction: "fedcba98abbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba",
            BlockHash: "01234567abbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba"));
        realtimeClient.Publish(new BitailsRealtimeEvent(
            BitailsRealtimeEventKind.BlockConnected,
            BitailsRealtimeTopicCatalog.ZmqHashBlock2Topic,
            DateTimeOffset.UtcNow,
            BlockHash: "76543210abbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba"));

        await removalCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await blockCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var removed = Assert.Single(removedMessages);
        Assert.Equal(RemoveFromMempoolReason.CollisionInBlockTx, removed.Reason);
        Assert.Equal("fedcba98abbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba", removed.CollidedWithTransaction);
        Assert.Equal("01234567abbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba", removed.BlockHash);
        Assert.Single(blockMessageBus.Messages);
        Assert.Equal("76543210abbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba", blockMessageBus.Messages.Single().BlockHash);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await runTask);
    }

    private sealed class FakeScopeProvider(string address) : IBitailsRealtimeSubscriptionScopeProvider
    {
        public Task<BitailsRealtimeSubscriptionScope> BuildAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new BitailsRealtimeSubscriptionScope(
                $"lock-address-{address}|spent-address-{address}",
                new BitailsRealtimeTransportPlan(
                    Dxs.Infrastructure.Bitails.Realtime.BitailsRealtimeTransportMode.WebSocket,
                    new Uri("https://api.bitails.io/global"),
                    [
                        $"lock-address-{address}",
                        $"spent-address-{address}"
                    ]),
                1,
                0,
                false));
    }

    private sealed class FakeBitailsRealtimeIngestClient : IBitailsRealtimeIngestClient
    {
        private readonly FakeConnection _connection = new();
        private readonly TaskCompletionSource _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ConnectCount { get; private set; }

        public Task<IBitailsRealtimeConnection> ConnectAsync(
            BitailsRealtimeTransportPlan plan,
            string apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ConnectCount++;
            _connected.TrySetResult();
            return Task.FromResult<IBitailsRealtimeConnection>(_connection);
        }

        public Task WaitForConnectAsync() => _connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Publish(BitailsRealtimeEvent realtimeEvent) => _connection.Publish(realtimeEvent);

        private sealed class FakeConnection : IBitailsRealtimeConnection
        {
            private readonly Subject<BitailsRealtimeEvent> _subject = new();

            public IObservable<BitailsRealtimeEvent> Events => _subject;

            public void Publish(BitailsRealtimeEvent realtimeEvent) => _subject.OnNext(realtimeEvent);

            public ValueTask DisposeAsync()
            {
                _subject.OnCompleted();
                _subject.Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FakeRawTransactionFetchService(string txId, byte[] raw) : IRawTransactionFetchService
    {
        public Task<RawTransactionFetchResult> GetAsync(string requestedTxId, CancellationToken cancellationToken = default)
            => TryGetAsync(requestedTxId, cancellationToken);

        public Task<RawTransactionFetchResult> TryGetAsync(string requestedTxId, CancellationToken cancellationToken = default)
            => Task.FromResult(
                string.Equals(requestedTxId, txId, StringComparison.OrdinalIgnoreCase)
                    ? new RawTransactionFetchResult(ExternalChainProviderName.Bitails, raw)
                    : null);
    }

    private sealed class FakeBlockMessageBus : IBlockMessageBus
    {
        private readonly BlockMessageBus _inner = new();

        public ConcurrentQueue<BlockMessage> Messages { get; } = new();

        public IDisposable AddPublisher(IObservable<BlockMessage> txObservable) => _inner.AddPublisher(txObservable);
        public IObservable<BlockMessage> AsObservable() => _inner.AsObservable();
        public IObserver<BlockMessage> AsObserver() => _inner.AsObserver();
        public void Post(BlockMessage message)
        {
            Messages.Enqueue(message);
            _inner.Post(message);
        }

        public IDisposable Subscribe(IObserver<BlockMessage> observer) => _inner.Subscribe(observer);
    }

    private sealed class RecordingObservationSink : ITxObservationSink
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _count;

        public Task RecordAsync(TxMessage message, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _count) >= 1)
                _completion.TrySetResult();

            return Task.CompletedTask;
        }

        public async Task WaitForCountAsync(int count)
        {
            if (_count >= count)
                return;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _completion.Task.WaitAsync(cts.Token);
        }
    }

    private sealed class RecordingTransactionStore : ITransactionStore
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _count;

        public ConcurrentQueue<string> SavedTransactions { get; } = new();

        public Task<List<Address>> GetWatchingAddresses() => Task.FromResult(new List<Address>());
        public Task<List<TokenId>> GetWatchingTokens() => Task.FromResult(new List<TokenId>());

        public Task<TransactionProcessStatus> SaveTransaction(
            Transaction transaction,
            long timestamp,
            string firstOutToRedeem,
            string blockHash = null,
            int? blockHeight = null,
            int? indexInBlock = null)
        {
            SavedTransactions.Enqueue(transaction.Id);
            if (Interlocked.Increment(ref _count) >= 1)
                _completion.TrySetResult();

            return Task.FromResult(TransactionProcessStatus.FoundInMempool);
        }

        public Task<Transaction> TryRemoveTransaction(string id) => Task.FromResult<Transaction>(default);

        public async Task WaitForCountAsync(int count)
        {
            if (_count >= count)
                return;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _completion.Task.WaitAsync(cts.Token);
        }
    }

    private sealed class TestNetworkProvider : INetworkProvider
    {
        public Network Network => Network.Mainnet;
    }

    private sealed class FakeProviderSettingsAccessor : IExternalChainProviderSettingsAccessor
    {
        public ValueTask<BitailsProviderRuntimeSettings> GetBitailsAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new BitailsProviderRuntimeSettings(
                "https://api.bitails.io",
                string.Empty,
                Dxs.Consigliere.Configs.BitailsRealtimeTransportMode.Websocket,
                "https://api.bitails.io/global",
                string.Empty,
                string.Empty));

        public ValueTask<WhatsOnChainProviderRuntimeSettings> GetWhatsOnChainAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new WhatsOnChainProviderRuntimeSettings(
                "https://api.whatsonchain.com/v1/bsv/main",
                string.Empty));

        public ValueTask<JungleBusProviderRuntimeSettings> GetJungleBusAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new JungleBusProviderRuntimeSettings(
                "https://junglebus.gorillapool.io",
                string.Empty,
                string.Empty,
                string.Empty));
    }
}
