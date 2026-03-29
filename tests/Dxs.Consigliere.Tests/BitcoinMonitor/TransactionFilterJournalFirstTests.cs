using System.Collections.Concurrent;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Impl;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;

using Microsoft.Extensions.Logging.Abstractions;

namespace Dxs.Consigliere.Tests.BitcoinMonitor;

public class TransactionFilterJournalFirstTests
{
    private const string SampleTransactionHex = "0100000001c6f4b6176d3f4d6c6d9e198ba89a4eb7a1b08e6a705cc8cf0f8f2f3e3bcedf1f000000006b4830450221009af2d63b8ef3ebf8c7a227327d8e1a89f5929087566bbb6d6f74a09a87e2375d022007f8cefa32f6d829bb3f8792dd11e5d8f1cb4e4f4f84f7a8d431fed0b8ff103a4121022b698a0f0a1f1fb43fb8f33c2d72cbe7f3f8d98ef1a304681140f64e5681970fffffffff02e8030000000000001976a91489abcdefabbaabbaabbaabbaabbaabbaabbaabba88ac0000000000000000066a040102030400000000";

    [Fact]
    public async Task AddedMempoolMessage_HitsObservationSinkOnlyAfterTrackedMatchIsSaved()
    {
        var txMessageBus = new TxMessageBus();
        var filteredBus = new FilteredTransactionMessageBus();
        var calls = new ConcurrentQueue<string>();
        var observationSink = new RecordingObservationSink(calls);
        var store = new RecordingTransactionStore(calls);

        using var filter = new TransactionFilter(
            txMessageBus,
            filteredBus,
            store,
            observationSink,
            NullLogger<TransactionFilter>.Instance
        );

        var transaction = Transaction.Parse(SampleTransactionHex, Network.Mainnet);
        filter.ManageUtxoSetForAddress(transaction.Outputs[0].Address);

        txMessageBus.Post(TxMessage.AddedToMempool(transaction, 1_710_000_000, TxObservationSource.Node));

        await observationSink.WaitForCountAsync(1);
        await store.WaitForCountAsync(1);

        Assert.Equal(
            ["store", "sink"],
            calls.ToArray()
        );
    }

    [Fact]
    public async Task UnmatchedMempoolMessage_DoesNotHitObservationSink()
    {
        var txMessageBus = new TxMessageBus();
        var filteredBus = new FilteredTransactionMessageBus();
        var calls = new ConcurrentQueue<string>();
        var observationSink = new RecordingObservationSink(calls);
        var store = new RecordingTransactionStore(calls);

        using var filter = new TransactionFilter(
            txMessageBus,
            filteredBus,
            store,
            observationSink,
            NullLogger<TransactionFilter>.Instance
        );

        var transaction = Transaction.Parse(SampleTransactionHex, Network.Mainnet);
        txMessageBus.Post(TxMessage.AddedToMempool(transaction, 1_710_000_000, TxObservationSource.Node));

        await Task.Delay(250);

        Assert.Empty(calls);
    }

    private sealed class RecordingObservationSink(ConcurrentQueue<string> calls) : ITxObservationSink
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _count;

        public Task RecordAsync(TxMessage message, CancellationToken cancellationToken = default)
        {
            calls.Enqueue("sink");

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

    private sealed class RecordingTransactionStore(ConcurrentQueue<string> calls) : ITransactionStore
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _count;

        public Task<List<Address>> GetWatchingAddresses()
            => Task.FromResult(new List<Address>());

        public Task<List<TokenId>> GetWatchingTokens()
            => Task.FromResult(new List<TokenId>());

        public Task<TransactionProcessStatus> SaveTransaction(
            Transaction transaction,
            long timestamp,
            string firstOutToRedeem,
            string blockHash = default!,
            int? blockHeight = null,
            int? indexInBlock = null
        )
        {
            calls.Enqueue("store");

            if (Interlocked.Increment(ref _count) >= 1)
                _completion.TrySetResult();

            return Task.FromResult(TransactionProcessStatus.FoundInMempool);
        }

        public Task<Transaction> TryRemoveTransaction(string id)
            => Task.FromResult<Transaction>(default!);

        public async Task WaitForCountAsync(int count)
        {
            if (_count >= count)
                return;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _completion.Task.WaitAsync(cts.Token);
        }
    }
}
