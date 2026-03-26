using System.Collections.Concurrent;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Dto.Responses.Readiness;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Notifications;

public sealed class ManagedScopeRealtimeNotifier(
    IDocumentStore documentStore,
    ITrackedEntityReadinessService readinessService,
    TxLifecycleProjectionRebuilder txLifecycleProjectionRebuilder,
    IRealtimeEventDispatcher realtimeEventDispatcher
)
{
    private static readonly TimeSpan ReadinessCacheTtl = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, CachedReadiness> _readinessCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ObservedTransactionScope> _observedTxScopes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TrackedEntitySnapshot> _trackedSnapshots = new(StringComparer.Ordinal);

    public Task SubscribeToTokenStream(string connectionId, string tokenId)
        => realtimeEventDispatcher.SubscribeToTokenStream(connectionId, tokenId);

    public Task UnsubscribeToTokenStream(string connectionId, string tokenId)
        => realtimeEventDispatcher.UnsubscribeToTokenStream(connectionId, tokenId);

    public async Task PublishTransactionSeenAsync(
        FilteredTransactionMessage message,
        CancellationToken cancellationToken = default
    )
    {
        var addresses = message.Addresses
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var tokenIds = message.Transaction.Outputs
            .Select(x => x.TokenId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        _observedTxScopes[message.Transaction.Id] = new ObservedTransactionScope(addresses, tokenIds);

        var transactionEvent = CreateEvent(
            eventId: $"rt:tx_seen:{message.Transaction.Id}",
            eventType: "tx_seen",
            entityType: "transaction",
            entityId: message.Transaction.Id,
            txId: message.Transaction.Id,
            blockHeight: null,
            authoritative: false,
            lifecycleStatus: TxLifecycleStatus.SeenBySource,
            payload: new Dictionary<string, object>
            {
                ["addresses"] = addresses,
                ["tokenIds"] = tokenIds
            }
        );

        foreach (var address in addresses)
        {
            await realtimeEventDispatcher.PublishToAddressAsync(address, transactionEvent);

            var readiness = await GetAddressReadinessAsync(address, cancellationToken);
            await realtimeEventDispatcher.PublishToAddressAsync(
                address,
                CreateEvent(
                    eventId: $"rt:balance_changed:{message.Transaction.Id}:{address}",
                    eventType: "balance_changed",
                    entityType: TrackedEntityType.Address,
                    entityId: address,
                    txId: message.Transaction.Id,
                    blockHeight: null,
                    authoritative: readiness.Authoritative,
                    lifecycleStatus: readiness.LifecycleStatus,
                    payload: new Dictionary<string, object>
                    {
                        ["address"] = address
                    }
                )
            );
        }

        foreach (var tokenId in tokenIds)
        {
            await realtimeEventDispatcher.PublishToTokenAsync(tokenId, transactionEvent);

            var readiness = await GetTokenReadinessAsync(tokenId, cancellationToken);
            await realtimeEventDispatcher.PublishToTokenAsync(
                tokenId,
                CreateEvent(
                    eventId: $"rt:token_state_changed:{message.Transaction.Id}:{tokenId}",
                    eventType: "token_state_changed",
                    entityType: TrackedEntityType.Token,
                    entityId: tokenId,
                    txId: message.Transaction.Id,
                    blockHeight: null,
                    authoritative: readiness.Authoritative,
                    lifecycleStatus: readiness.LifecycleStatus,
                    payload: new Dictionary<string, object>
                    {
                        ["tokenId"] = tokenId
                    }
                )
            );
        }
    }

    public async Task PublishTransactionDeletedAsync(
        string txId,
        CancellationToken cancellationToken = default
    )
    {
        await txLifecycleProjectionRebuilder.RebuildAsync(cancellationToken: cancellationToken);

        using var session = documentStore.GetNoCacheNoTrackingSession();
        var lifecycle = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(txId), cancellationToken);
        var scope = _observedTxScopes.TryGetValue(txId, out var cached)
            ? cached
            : await TryLoadScopeFromMetaTransactionAsync(session, txId, cancellationToken) ?? new ObservedTransactionScope([], []);

        var eventType = string.Equals(lifecycle?.LifecycleStatus, TxLifecycleStatus.Reorged, StringComparison.Ordinal)
            ? "tx_reorged"
            : "tx_dropped";

        var realtimeEvent = CreateEvent(
            eventId: $"rt:{eventType}:{txId}",
            eventType: eventType,
            entityType: "transaction",
            entityId: txId,
            txId: txId,
            blockHeight: lifecycle?.BlockHeight,
            authoritative: lifecycle?.Authoritative ?? false,
            lifecycleStatus: lifecycle?.LifecycleStatus ?? TxLifecycleStatus.Dropped,
            payload: new Dictionary<string, object>
            {
                ["txId"] = txId
            }
        );

        foreach (var address in scope.Addresses)
            await realtimeEventDispatcher.PublishToAddressAsync(address, realtimeEvent);

        foreach (var tokenId in scope.TokenIds)
            await realtimeEventDispatcher.PublishToTokenAsync(tokenId, realtimeEvent);
    }

    public async Task PublishBlockProcessedAsync(
        int height,
        string hash,
        CancellationToken cancellationToken = default
    )
    {
        await txLifecycleProjectionRebuilder.RebuildAsync(cancellationToken: cancellationToken);

        using var session = documentStore.GetNoCacheNoTrackingSession();
        var confirmed = await session.Query<TxLifecycleProjectionDocument>()
            .Where(x => x.BlockHash == hash && x.LifecycleStatus == TxLifecycleStatus.Confirmed)
            .ToListAsync(token: cancellationToken);

        foreach (var projection in confirmed)
        {
            var scope = _observedTxScopes.TryGetValue(projection.TxId, out var cached)
                ? cached
                : await TryLoadScopeFromMetaTransactionAsync(session, projection.TxId, cancellationToken);
            if (scope is null)
                continue;

            var realtimeEvent = CreateEvent(
                eventId: $"rt:tx_confirmed:{projection.TxId}:{hash}",
                eventType: "tx_confirmed",
                entityType: "transaction",
                entityId: projection.TxId,
                txId: projection.TxId,
                blockHeight: projection.BlockHeight ?? height,
                authoritative: projection.Authoritative,
                lifecycleStatus: projection.LifecycleStatus,
                payload: new Dictionary<string, object>
                {
                    ["blockHash"] = hash
                }
            );

            foreach (var address in scope.Addresses)
                await realtimeEventDispatcher.PublishToAddressAsync(address, realtimeEvent);

            foreach (var tokenId in scope.TokenIds)
                await realtimeEventDispatcher.PublishToTokenAsync(tokenId, realtimeEvent);
        }

        await PublishTrackedLifecycleChangesAsync(cancellationToken);
    }

    private async Task PublishTrackedLifecycleChangesAsync(CancellationToken cancellationToken)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var current = await LoadTrackedSnapshotsAsync(session, cancellationToken);

        if (_trackedSnapshots.IsEmpty)
        {
            foreach (var pair in current)
                _trackedSnapshots[pair.Key] = pair.Value;
            return;
        }

        foreach (var pair in current)
        {
            if (!_trackedSnapshots.TryGetValue(pair.Key, out var previous))
            {
                _trackedSnapshots[pair.Key] = pair.Value;
                continue;
            }

            var snapshot = pair.Value;
            if (snapshot.Equals(previous))
                continue;

            await PublishScopeEventAsync(
                snapshot,
                "scope_status_changed",
                new Dictionary<string, object>
                {
                    ["fromStatus"] = previous.LifecycleStatus,
                    ["toStatus"] = snapshot.LifecycleStatus
                }
            );

            if (!string.Equals(previous.LifecycleStatus, TrackedEntityLifecycleStatus.Live, StringComparison.Ordinal)
                && string.Equals(snapshot.LifecycleStatus, TrackedEntityLifecycleStatus.Live, StringComparison.Ordinal))
            {
                await PublishScopeEventAsync(
                    snapshot,
                    "scope_caught_up",
                    new Dictionary<string, object>
                    {
                        ["status"] = snapshot.LifecycleStatus
                    }
                );
            }

            if ((!previous.Degraded && snapshot.Degraded)
                || (!string.Equals(previous.LifecycleStatus, TrackedEntityLifecycleStatus.Degraded, StringComparison.Ordinal)
                    && string.Equals(snapshot.LifecycleStatus, TrackedEntityLifecycleStatus.Degraded, StringComparison.Ordinal)))
            {
                await PublishScopeEventAsync(
                    snapshot,
                    "scope_degraded",
                    new Dictionary<string, object>
                    {
                        ["previousStatus"] = previous.LifecycleStatus,
                        ["newStatus"] = snapshot.LifecycleStatus
                    }
                );
            }

            _trackedSnapshots[pair.Key] = snapshot;
        }
    }

    private Task PublishScopeEventAsync(
        TrackedEntitySnapshot snapshot,
        string eventType,
        Dictionary<string, object> payload
    )
    {
        var realtimeEvent = CreateEvent(
            eventId: $"rt:{eventType}:{snapshot.EntityType}:{snapshot.EntityId}:{snapshot.LifecycleStatus}",
            eventType: eventType,
            entityType: snapshot.EntityType,
            entityId: snapshot.EntityId,
            txId: null,
            blockHeight: snapshot.LagBlocks,
            authoritative: snapshot.Authoritative,
            lifecycleStatus: snapshot.LifecycleStatus,
            payload: payload
        );

        return string.Equals(snapshot.EntityType, TrackedEntityType.Address, StringComparison.Ordinal)
            ? realtimeEventDispatcher.PublishToAddressAsync(snapshot.EntityId, realtimeEvent)
            : realtimeEventDispatcher.PublishToTokenAsync(snapshot.EntityId, realtimeEvent);
    }

    private async Task<TrackedRealtimeReadiness> GetAddressReadinessAsync(string address, CancellationToken cancellationToken)
        => TrackedRealtimeReadiness.From(await GetCachedReadinessAsync(
            $"address:{address}",
            () => readinessService.GetAddressReadinessAsync(address, cancellationToken)
        ));

    private async Task<TrackedRealtimeReadiness> GetTokenReadinessAsync(string tokenId, CancellationToken cancellationToken)
        => TrackedRealtimeReadiness.From(await GetCachedReadinessAsync(
            $"token:{tokenId}",
            () => readinessService.GetTokenReadinessAsync(tokenId, cancellationToken)
        ));

    private async Task<TrackedEntityReadinessResponse> GetCachedReadinessAsync(
        string key,
        Func<Task<TrackedEntityReadinessResponse>> factory
    )
    {
        if (_readinessCache.TryGetValue(key, out var cached)
            && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Value;
        }

        var readiness = await factory();
        _readinessCache[key] = new CachedReadiness(DateTimeOffset.UtcNow.Add(ReadinessCacheTtl), readiness);
        return readiness;
    }

    private static RealtimeEventResponse CreateEvent(
        string eventId,
        string eventType,
        string entityType,
        string entityId,
        string txId,
        int? blockHeight,
        bool authoritative,
        string lifecycleStatus,
        Dictionary<string, object> payload
    ) => new()
    {
        EventId = eventId,
        EventType = eventType,
        EntityType = entityType,
        EntityId = entityId,
        TxId = txId,
        BlockHeight = blockHeight,
        Timestamp = DateTimeOffset.UtcNow,
        Authoritative = authoritative,
        LifecycleStatus = lifecycleStatus,
        Payload = payload
    };

    private static async Task<ObservedTransactionScope> TryLoadScopeFromMetaTransactionAsync(
        IAsyncDocumentSession session,
        string txId,
        CancellationToken cancellationToken
    )
    {
        var transaction = await session.LoadAsync<MetaTransaction>(txId, cancellationToken);
        return transaction is null
            ? null
            : new ObservedTransactionScope(
                transaction.Addresses?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray() ?? [],
                transaction.TokenIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray() ?? []
            );
    }

    private static async Task<Dictionary<string, TrackedEntitySnapshot>> LoadTrackedSnapshotsAsync(
        IAsyncDocumentSession session,
        CancellationToken cancellationToken
    )
    {
        var addressStatuses = await session.Query<TrackedAddressStatusDocument>()
            .Where(x => x.Tracked && !x.IsTombstoned)
            .ToListAsync(token: cancellationToken);
        var tokenStatuses = await session.Query<TrackedTokenStatusDocument>()
            .Where(x => x.Tracked && !x.IsTombstoned)
            .ToListAsync(token: cancellationToken);

        return addressStatuses
            .Cast<TrackedEntityDocumentBase>()
            .Concat(tokenStatuses)
            .ToDictionary(
                x => $"{x.EntityType}:{x.EntityId}",
                TrackedEntitySnapshot.From,
                StringComparer.Ordinal
            );
    }

    private readonly record struct CachedReadiness(DateTimeOffset ExpiresAt, TrackedEntityReadinessResponse Value);

    private sealed record ObservedTransactionScope(string[] Addresses, string[] TokenIds);

    private readonly record struct TrackedRealtimeReadiness(string LifecycleStatus, bool Authoritative)
    {
        public static TrackedRealtimeReadiness From(TrackedEntityReadinessResponse readiness)
            => new(readiness.LifecycleStatus, readiness.Authoritative);
    }

    private readonly record struct TrackedEntitySnapshot(
        string EntityType,
        string EntityId,
        string LifecycleStatus,
        bool Readable,
        bool Authoritative,
        bool Degraded,
        int? LagBlocks,
        double? Progress
    )
    {
        public static TrackedEntitySnapshot From(TrackedEntityDocumentBase document)
            => new(
                document.EntityType,
                document.EntityId,
                document.LifecycleStatus,
                document.Readable,
                document.Authoritative,
                document.Degraded,
                document.LagBlocks,
                document.Progress
            );
    }
}
