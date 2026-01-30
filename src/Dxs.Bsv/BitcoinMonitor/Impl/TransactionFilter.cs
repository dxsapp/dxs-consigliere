using System;
using System.Collections.Concurrent;
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
    private readonly ILogger _logger;

    private readonly CancellationTokenSource _cts = new();
    private readonly Timer _periodicLogger;

    private int _transactionsCounter;
    private int _foundInMempoolCounter;
    private int _foundInBlockCounter;
    private int _updatedOnBlockConnectedCounter;
    private int _reFoundInMempoolCounter;
    private int _notModified;
    private readonly ConcurrentDictionary<string, Address> _watchingAddresses = new();
    private readonly ConcurrentDictionary<string, TokenId> _watchingTokens = new();
    private readonly ConcurrentDictionary<string, Address> _watchingTokensRedeemAddresses = new();

    private IAgent<TxMessage> _messageHandler;
    private IDisposable _busSub;

    public TransactionFilter(
        ITxMessageBus txMessageBus,
        IFilteredTransactionMessageBus filteredTransactionMessageBus,
        ITransactionStore transactionStore,
        ILogger<TransactionFilter> logger
    )
    {
        _txMessageBus = txMessageBus;
        _filteredTransactionMessageBus = filteredTransactionMessageBus;
        _transactionStore = transactionStore;
        _logger = logger;

        _periodicLogger = new Timer(LogCount, null, LogPeriod, LogPeriod);

        InitAsync().WaitAndUnwrapException();
    }

    #region IUtxoMonitor


    public void ManageUtxoSetForAddress(Address address)
        => _watchingAddresses.TryAdd(address.Value, address);

    public void ManageUtxoSetForToken(TokenId tokenId)
    {
        _watchingTokens.TryAdd(tokenId.Value, tokenId);
        _watchingTokensRedeemAddresses.TryAdd(tokenId.RedeemAddress.Value, tokenId.RedeemAddress);
    }

    public int QueueLength() => _messageHandler.MessagesInQueue;

    #endregion

    #region .pvt


    private async Task InitAsync()
    {
        foreach (var address in await _transactionStore.GetWatchingAddresses())
            _watchingAddresses.TryAdd(address.Value, address);

        foreach (var tokenId in await _transactionStore.GetWatchingTokens())
        {
            _watchingTokens.TryAdd(tokenId.Value, tokenId);
            _watchingTokensRedeemAddresses.TryAdd(tokenId.RedeemAddress.Value, tokenId.RedeemAddress);
        }

        _messageHandler = Agent.Start<TxMessage>(Handle);
        _busSub = _txMessageBus.Subscribe(
            message => _messageHandler.Post(message),
            exception => _logger.LogError(exception, "Error during txMessage handling")
        );

        _logger.LogDebug(
            "Transaction filter started with {@InitState}",
            new
            {
                WatchingAddresses = _watchingAddresses.Count,
                WatchingTokens = _watchingTokens.Count,
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

    private readonly HashSet<string> _addresses = new();

    private async Task HandleAddTransaction(TxMessage message)
    {
        var transaction = message.Transaction;
        var save = false;
        var redeemAddress = (string)null;

        foreach (var output in transaction.Outputs)
        {
            if (output.Address?.Value is { } address)
            {
                if (_watchingAddresses.ContainsKey(address))
                {
                    save = true;
                    _addresses.Add(address);
                }

                if (output.Idx == 0
                    && _watchingTokensRedeemAddresses.ContainsKey(address)) //monitor manages the token, but does not manage redeem address
                {
                    save = true;
                    redeemAddress = address;
                }
            }

            if (output.Type == ScriptType.P2STAS &&
                output.TokenId.IsNotNullOrEmpty() &&
                _watchingTokens.ContainsKey(output.TokenId))
            {
                save = true;

                _addresses.Add(output.Address!.Value);
            }
        }

        foreach (var input in transaction.Inputs)
        {
            if (input.Address == null) continue;

            if (!save)
            {
                if (_watchingAddresses.ContainsKey(input.Address.Value))
                {
                    save = true;
                }
            }

            _addresses.Add(input.Address.Value);
        }

        if (save)
        {
            var status = await _transactionStore.SaveTransaction(
                transaction,
                message.Timestamp,
                redeemAddress,
                message.BlockHash,
                message.Height,
                message.Idx
            );

            if (status == TransactionProcessStatus.Unexpected)
                _logger.LogError("SaveTransaction {TransactionId} returned status Unexpected", transaction.Id);
            else if (status == TransactionProcessStatus.FoundInMempool)
                _foundInMempoolCounter++;
            else if (status == TransactionProcessStatus.FoundInBlock)
                _foundInBlockCounter++;
            else if (status == TransactionProcessStatus.UpdatedOnBlockConnected)
                _updatedOnBlockConnectedCounter++;
            else if (status == TransactionProcessStatus.ReFoundInMempool)
                _reFoundInMempoolCounter++;
            else if (status == TransactionProcessStatus.NotModified)
                _notModified++;

            _logger.LogDebug("{TransactionId} processed with status {Status:G}", transaction.Id, status);

            var addresses = _addresses.Any()
                ? new HashSet<string>(_addresses)
                : null;
            var txMessage = new FilteredTransactionMessage(transaction, addresses);

            _filteredTransactionMessageBus.Post(txMessage);
        }

        _addresses.Clear();

        _transactionsCounter++;
    }

    private Task HandleRemoveTransaction(TxMessage message) => _transactionStore.TryRemoveTransaction(message.TxId);

    private void LogCount(object state)
    {
        _logger.LogInformation("{@HandledTransactions}", new
        {
            All = Interlocked.Exchange(ref _transactionsCounter, 0),
            FoundInMempool = Interlocked.Exchange(ref _foundInMempoolCounter, 0),
            FoundInBlock = Interlocked.Exchange(ref _foundInBlockCounter, 0),
            UpdatedOnBlockConnected = Interlocked.Exchange(ref _updatedOnBlockConnectedCounter, 0),
            ReFoundInMempool = Interlocked.Exchange(ref _reFoundInMempoolCounter, 0),
            NotModified = Interlocked.Exchange(ref _notModified, 0),
        });
    }

    #endregion


    public void Dispose()
    {
        _cts?.Dispose();
        _busSub?.Dispose();
        _periodicLogger.Dispose();
    }
}
