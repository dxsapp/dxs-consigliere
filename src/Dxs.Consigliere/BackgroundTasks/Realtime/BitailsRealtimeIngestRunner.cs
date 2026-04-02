using System.Collections.Concurrent;
using System.Reactive.Linq;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Services;
using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.Bitails.Realtime;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Logging;

namespace Dxs.Consigliere.BackgroundTasks.Realtime;

public sealed class BitailsRealtimeIngestRunner(
    IBitailsRealtimeIngestClient realtimeIngestClient,
    IBitailsRealtimeSubscriptionScopeProvider scopeProvider,
    IRawTransactionFetchService rawTransactionFetchService,
    IExternalChainProviderSettingsAccessor providerSettingsAccessor,
    INetworkProvider networkProvider,
    ITxMessageBus txMessageBus,
    IBlockMessageBus blockMessageBus,
    ILogger<BitailsRealtimeIngestRunner> logger
)
{
    private static readonly TimeSpan RefreshPeriod = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RecentlySeenRetention = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _recentlySeen = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _logger = logger;
    private int _handledMessages;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        BitailsRealtimeSubscriptionScope? activeScope = null;
        IBitailsRealtimeConnection activeConnection = null;
        IDisposable activeSubscription = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var scope = await scopeProvider.BuildAsync(cancellationToken);
                if (scope.TransportPlan.Topics.Count == 0)
                {
                    activeSubscription?.Dispose();
                    activeSubscription = null;
                    if (activeConnection is not null)
                    {
                        await activeConnection.DisposeAsync();
                        activeConnection = null;
                    }

                    activeScope = null;
                    await Task.Delay(RefreshPeriod, cancellationToken);
                    continue;
                }

                if (activeScope is null || !string.Equals(activeScope.Signature, scope.Signature, StringComparison.Ordinal))
                {
                    activeSubscription?.Dispose();
                    activeSubscription = null;
                    if (activeConnection is not null)
                    {
                        await activeConnection.DisposeAsync();
                        activeConnection = null;
                    }

                    var bitailsSettings = await providerSettingsAccessor.GetBitailsAsync(cancellationToken);
                    activeConnection = await realtimeIngestClient.ConnectAsync(scope.TransportPlan, bitailsSettings.ApiKey, cancellationToken);
                    activeSubscription = activeConnection.Events
                        .Subscribe(realtimeEvent => _ = HandleEventAsync(realtimeEvent, cancellationToken));
                    activeScope = scope;

                    _logger.LogInformation(
                        "Bitails realtime ingest active with {TopicCount} topics; addresses={AddressCount}; tokens={TokenCount}; globalTx={UsesGlobalTransactions}",
                        scope.TransportPlan.Topics.Count,
                        scope.AddressCount,
                        scope.TokenCount,
                        scope.UsesGlobalTransactions);
                }

                TrimRecentlySeen();
                await Task.Delay(RefreshPeriod, cancellationToken);
            }
        }
        finally
        {
            activeSubscription?.Dispose();
            if (activeConnection is not null)
                await activeConnection.DisposeAsync();
        }
    }

    private async Task HandleEventAsync(BitailsRealtimeEvent realtimeEvent, CancellationToken cancellationToken)
    {
        switch (realtimeEvent.Kind)
        {
            case BitailsRealtimeEventKind.TransactionAdded:
                await HandleTransactionAddedAsync(realtimeEvent, cancellationToken);
                return;
            case BitailsRealtimeEventKind.TransactionRemoved:
                HandleTransactionRemoved(realtimeEvent);
                return;
            case BitailsRealtimeEventKind.BlockConnected:
                HandleBlockConnected(realtimeEvent);
                return;
            default:
                _logger.LogDebug("Ignoring unsupported Bitails realtime event kind {Kind} on topic {Topic}", realtimeEvent.Kind, realtimeEvent.Topic);
                return;
        }
    }

    private async Task HandleTransactionAddedAsync(BitailsRealtimeEvent realtimeEvent, CancellationToken cancellationToken)
    {
        string reservedTxId = null;

        try
        {
            Transaction transaction;
            if (realtimeEvent.RawTransaction is { Length: > 0 } rawTransaction)
            {
                transaction = Transaction.Parse(rawTransaction, networkProvider.Network);
                if (!TryReserve(transaction.Id))
                    return;

                reservedTxId = transaction.Id;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(realtimeEvent.TxId) || !TryReserve(realtimeEvent.TxId))
                    return;

                reservedTxId = realtimeEvent.TxId;
                var raw = await rawTransactionFetchService.TryGetAsync(realtimeEvent.TxId, cancellationToken);
                if (raw?.Raw is not { Length: > 0 } txRaw)
                {
                    _recentlySeen.TryRemove(realtimeEvent.TxId, out _);
                    _logger.LogDebug("Bitails realtime tx {TxId} was announced on {Topic} but raw payload was unavailable", realtimeEvent.TxId, realtimeEvent.Topic);
                    return;
                }

                transaction = Transaction.Parse(txRaw, networkProvider.Network);
            }

            txMessageBus.Post(TxMessage.AddedToMempool(
                transaction,
                realtimeEvent.ObservedAt.ToUnixTimeSeconds(),
                TxObservationSource.Bitails
            ));

            var handled = Interlocked.Increment(ref _handledMessages);
            if (handled % 100 == 0)
                _logger.LogDebug("Bitails realtime ingest handled {Count} transactions", handled);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(reservedTxId))
                _recentlySeen.TryRemove(reservedTxId, out _);
            throw;
        }
    }

    private void HandleTransactionRemoved(BitailsRealtimeEvent realtimeEvent)
    {
        if (string.IsNullOrWhiteSpace(realtimeEvent.TxId))
        {
            _logger.LogDebug("Ignoring Bitails realtime remove event on {Topic} because txid was missing", realtimeEvent.Topic);
            return;
        }

        txMessageBus.Post(TxMessage.RemovedFromMempool(
            realtimeEvent.TxId,
            TxObservationSource.Bitails,
            MapRemoveReason(realtimeEvent.RemoveReason),
            realtimeEvent.CollidedWithTransaction,
            realtimeEvent.BlockHash));
    }

    private void HandleBlockConnected(BitailsRealtimeEvent realtimeEvent)
    {
        if (string.IsNullOrWhiteSpace(realtimeEvent.BlockHash))
        {
            _logger.LogDebug("Ignoring Bitails realtime block event on {Topic} because block hash was missing", realtimeEvent.Topic);
            return;
        }

        blockMessageBus.Post(new BlockMessage(realtimeEvent.BlockHash, TxObservationSource.Bitails));
    }

    private bool TryReserve(string txId)
        => _recentlySeen.TryAdd(txId, DateTimeOffset.UtcNow);

    private void TrimRecentlySeen()
    {
        if (_recentlySeen.Count < 1024)
            return;

        var cutoff = DateTimeOffset.UtcNow - RecentlySeenRetention;
        foreach (var entry in _recentlySeen)
        {
            if (entry.Value < cutoff)
                _recentlySeen.TryRemove(entry.Key, out _);
        }
    }

    private static RemoveFromMempoolReason MapRemoveReason(string reason)
        => reason?.Trim().ToLowerInvariant() switch
        {
            "expired" => RemoveFromMempoolReason.Expired,
            "mempool-sizelimit-exceeded" => RemoveFromMempoolReason.MempoolSizeLimitExceeded,
            "collision-in-block-tx" => RemoveFromMempoolReason.CollisionInBlockTx,
            "reorg" => RemoveFromMempoolReason.Reorg,
            "included-in-block" => RemoveFromMempoolReason.IncludedInBlock,
            _ => RemoveFromMempoolReason.Unknown
        };
}
