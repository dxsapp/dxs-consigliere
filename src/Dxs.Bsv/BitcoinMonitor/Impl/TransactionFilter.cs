using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Script;
using Dxs.Common.Dataflow;
using Dxs.Common.Extensions;

using Microsoft.Extensions.Logging;

using Nito.AsyncEx.Synchronous;

namespace Dxs.Bsv.BitcoinMonitor.Impl;

public class TransactionFilter : ITransactionFilter
{
    private static readonly TimeSpan LogPeriod = TimeSpan.FromMinutes(1);

    private readonly ITxMessageBus _txMessageBus;
    private readonly IFilteredTransactionMessageBus _filteredTransactionMessageBus;
    private readonly ITransactionStore _transactionStore;
    private readonly ITxObservationSink _txObservationSink;
    private readonly ILogger _logger;
    private readonly TransactionFilterWatchSet _watchSet = new();
    private readonly TransactionFilterMetrics _metrics = new();

    private readonly CancellationTokenSource _cts = new();
    private readonly Timer _periodicLogger;

    private IAgent<TxMessage> _messageHandler;
    private IDisposable _busSub;

    public TransactionFilter(
        ITxMessageBus txMessageBus,
        IFilteredTransactionMessageBus filteredTransactionMessageBus,
        ITransactionStore transactionStore,
        ITxObservationSink txObservationSink,
        ILogger<TransactionFilter> logger
    )
    {
        _txMessageBus = txMessageBus;
        _filteredTransactionMessageBus = filteredTransactionMessageBus;
        _transactionStore = transactionStore;
        _txObservationSink = txObservationSink;
        _logger = logger;

        _periodicLogger = new Timer(LogCount, null, LogPeriod, LogPeriod);

        InitAsync().WaitAndUnwrapException();
    }

    #region IUtxoMonitor


    public void ManageUtxoSetForAddress(Address address)
        => _watchSet.AddAddress(address);

    public void ManageUtxoSetForToken(TokenId tokenId)
        => _watchSet.AddToken(tokenId);

    public void UnmanageUtxoSetForAddress(Address address)
        => _watchSet.RemoveAddress(address);

    public void UnmanageUtxoSetForToken(TokenId tokenId)
        => _watchSet.RemoveToken(tokenId);

    public int QueueLength() => _messageHandler.MessagesInQueue;

    #endregion

    #region .pvt


    private async Task InitAsync()
    {
        _watchSet.SeedAddresses(await _transactionStore.GetWatchingAddresses());
        _watchSet.SeedTokens(await _transactionStore.GetWatchingTokens());

        _messageHandler = Agent.Start<TxMessage>(Handle);
        _busSub = _txMessageBus.Subscribe(
            message => _messageHandler.Post(message),
            exception => _logger.LogError(exception, "Error during txMessage handling")
        );

        _logger.LogDebug(
            "Transaction filter started with {@InitState}",
            new
            {
                WatchingAddresses = _watchSet.WatchingAddressesCount,
                WatchingTokens = _watchSet.WatchingTokensCount,
            }
        );
    }

    private async Task Handle(TxMessage message)
    {
        try
        {
            switch (message.MessageType)
            {
                case TxMessage.Type.FoundInBlock:
                case TxMessage.Type.AddedToMempool:
                    await HandleAddTransaction(message);
                    break;
                case TxMessage.Type.RemoveTransaction when message.Reason != RemoveFromMempoolReason.IncludedInBlock:
                    await HandleRemoveTransaction(message);
                    break;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to handle message: {@Message}", message);
        }
    }

    private async Task HandleAddTransaction(TxMessage message)
    {
        var transaction = message.Transaction;
        var match = _watchSet.Match(transaction);

        if (match.Save)
        {
            var status = await _transactionStore.SaveTransaction(
                transaction,
                message.Timestamp,
                match.RedeemAddress,
                message.BlockHash,
                message.Height,
                message.Idx
            );

            if (status == TransactionProcessStatus.Unexpected)
                _logger.LogError("SaveTransaction {TransactionId} returned status Unexpected", transaction.Id);
            else
                _metrics.Observe(status);

            _logger.LogDebug("{TransactionId} processed with status {Status:G}", transaction.Id, status);

            await _txObservationSink.RecordAsync(message, _cts.Token);

            var txMessage = new FilteredTransactionMessage(transaction, match.Addresses, message);

            _filteredTransactionMessageBus.Post(txMessage);
        }

        _metrics.IncrementProcessed();
    }

    private Task HandleRemoveTransaction(TxMessage message) => _transactionStore.TryRemoveTransaction(message.TxId);

    private void LogCount(object state)
        => _logger.LogInformation("{@HandledTransactions}", _metrics.SnapshotAndReset());

    #endregion


    public void Dispose()
    {
        _cts?.Dispose();
        _busSub?.Dispose();
        _periodicLogger.Dispose();
    }
}
