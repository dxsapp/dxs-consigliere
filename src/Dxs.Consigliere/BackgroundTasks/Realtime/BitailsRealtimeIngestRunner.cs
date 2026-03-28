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
    IBitailsRestApiClient restApiClient,
    IExternalChainProviderSettingsAccessor providerSettingsAccessor,
    INetworkProvider networkProvider,
    ITxMessageBus txMessageBus,
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
                    if (scope.TransportPlan.Mode != Dxs.Infrastructure.Bitails.Realtime.BitailsRealtimeTransportMode.WebSocket)
                    {
                        throw new NotSupportedException(
                            $"Bitails realtime ingest currently supports only {Dxs.Infrastructure.Bitails.Realtime.BitailsRealtimeTransportMode.WebSocket:G} transport.");
                    }

                    activeSubscription?.Dispose();
                    activeSubscription = null;
                    if (activeConnection is not null)
                    {
                        await activeConnection.DisposeAsync();
                        activeConnection = null;
                    }

                    var bitailsSettings = await providerSettingsAccessor.GetBitailsAsync(cancellationToken);
                    activeConnection = await realtimeIngestClient.ConnectAsync(scope.TransportPlan, bitailsSettings.ApiKey, cancellationToken);
                    activeSubscription = activeConnection.Transactions
                        .Subscribe(notification => _ = HandleNotificationAsync(notification, cancellationToken));
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

    private async Task HandleNotificationAsync(BitailsRealtimeTransactionNotification notification, CancellationToken cancellationToken)
    {
        if (!TryReserve(notification.TxId))
            return;

        try
        {
            var raw = await restApiClient.GetTransactionRawOrNullAsync(notification.TxId, cancellationToken);
            if (raw is null || raw.Length == 0)
            {
                _recentlySeen.TryRemove(notification.TxId, out _);
                _logger.LogDebug("Bitails realtime tx {TxId} was announced on {Topic} but raw payload was unavailable", notification.TxId, notification.Topic);
                return;
            }

            var transaction = Transaction.Parse(raw, networkProvider.Network);
            txMessageBus.Post(TxMessage.AddedToMempool(
                transaction,
                notification.ObservedAt.ToUnixTimeSeconds(),
                TxObservationSource.Bitails
            ));

            var handled = Interlocked.Increment(ref _handledMessages);
            if (handled % 100 == 0)
                _logger.LogDebug("Bitails realtime ingest handled {Count} transactions", handled);
        }
        catch
        {
            _recentlySeen.TryRemove(notification.TxId, out _);
            throw;
        }
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
}
