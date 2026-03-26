using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Notifications;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using Raven.Client.Documents;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Notifications;

public class ManagedScopeRealtimeNotifierTests : RavenTestDriver
{
    private const string Address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    private const string TokenId = "1111111111111111111111111111111111111111";
    private const string TxId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task PublishTransactionSeenAsync_EmitsTxSeenBalanceAndTokenStateEvents()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTrackedAddressAsync(store, TrackedEntityLifecycleStatus.Live, readable: true, authoritative: true, degraded: false);
        await SeedTrackedTokenAsync(store, TrackedEntityLifecycleStatus.Live, readable: true, authoritative: true, degraded: false);

        var dispatcher = new FakeRealtimeEventDispatcher();
        var notifier = BuildNotifier(store, dispatcher);

        await notifier.PublishTransactionSeenAsync(CreateMessage(TxId, Address, TokenId));

        Assert.Contains(dispatcher.AddressEvents[Address], x => x.EventType == "tx_seen" && x.EntityType == "transaction");
        Assert.Contains(dispatcher.AddressEvents[Address], x => x.EventType == "balance_changed" && x.EntityType == "address");
        Assert.Contains(dispatcher.TokenEvents[TokenId], x => x.EventType == "tx_seen" && x.EntityType == "transaction");
        Assert.Contains(dispatcher.TokenEvents[TokenId], x => x.EventType == "token_state_changed" && x.EntityType == "token");
    }

    [Fact]
    public async Task PublishBlockProcessedAsync_EmitsScopeStatusTransitions()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTrackedAddressAsync(store, TrackedEntityLifecycleStatus.CatchingUp, readable: false, authoritative: false, degraded: false);

        var dispatcher = new FakeRealtimeEventDispatcher();
        var notifier = BuildNotifier(store, dispatcher);

        await notifier.PublishBlockProcessedAsync(100, "block-1");

        await SeedTrackedAddressAsync(store, TrackedEntityLifecycleStatus.Live, readable: true, authoritative: true, degraded: false);
        await notifier.PublishBlockProcessedAsync(101, "block-2");

        Assert.Contains(dispatcher.AddressEvents[Address], x => x.EventType == "scope_status_changed");
        Assert.Contains(dispatcher.AddressEvents[Address], x => x.EventType == "scope_caught_up");
    }

    [Fact]
    public async Task PublishBlockProcessedAsync_EmitsTxConfirmedForTrackedScopes()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTrackedAddressAsync(store, TrackedEntityLifecycleStatus.Live, readable: true, authoritative: true, degraded: false);
        await SeedTrackedTokenAsync(store, TrackedEntityLifecycleStatus.Live, readable: true, authoritative: true, degraded: false);
        await SeedMetaTransactionAsync(store, TxId, Address, TokenId);
        await SeedLifecycleProjectionAsync(store, TxId, TxLifecycleStatus.Confirmed, "block-3", 303);

        var dispatcher = new FakeRealtimeEventDispatcher();
        var notifier = BuildNotifier(store, dispatcher);

        await notifier.PublishBlockProcessedAsync(303, "block-3");

        Assert.Contains(dispatcher.AddressEvents[Address], x => x.EventType == "tx_confirmed" && x.BlockHeight == 303);
        Assert.Contains(dispatcher.TokenEvents[TokenId], x => x.EventType == "tx_confirmed" && x.BlockHeight == 303);
    }

    [Fact]
    public async Task PublishTransactionDeletedAsync_EmitsTxReorgedWhenLifecycleProjectionSaysSo()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTrackedAddressAsync(store, TrackedEntityLifecycleStatus.Live, readable: true, authoritative: true, degraded: false);
        await SeedTrackedTokenAsync(store, TrackedEntityLifecycleStatus.Live, readable: true, authoritative: true, degraded: false);
        await SeedLifecycleProjectionAsync(store, TxId, TxLifecycleStatus.Reorged, null, null);

        var dispatcher = new FakeRealtimeEventDispatcher();
        var notifier = BuildNotifier(store, dispatcher);
        await notifier.PublishTransactionSeenAsync(CreateMessage(TxId, Address, TokenId));

        await notifier.PublishTransactionDeletedAsync(TxId);

        Assert.Contains(dispatcher.AddressEvents[Address], x => x.EventType == "tx_reorged");
        Assert.Contains(dispatcher.TokenEvents[TokenId], x => x.EventType == "tx_reorged");
    }

    private static ManagedScopeRealtimeNotifier BuildNotifier(IDocumentStore store, FakeRealtimeEventDispatcher dispatcher)
    {
        var journalReader = new RavenObservationJournalReader(store);
        var txRebuilder = new TxLifecycleProjectionRebuilder(store, journalReader);

        return new ManagedScopeRealtimeNotifier(
            store,
            new TrackedEntityReadinessService(store),
            txRebuilder,
            dispatcher
        );
    }

    private static FilteredTransactionMessage CreateMessage(string txId, string address, string tokenId)
    {
        var tx = new Transaction(Network.Mainnet)
        {
            Id = txId,
            Raw = [0x00]
        };
        tx.Outputs.Add(new Output
        {
            Address = new Address(address),
            TokenId = tokenId,
            Type = ScriptType.P2STAS,
            Satoshis = 1,
            Idx = 0,
            ScriptPubKey = default
        });

        return new FilteredTransactionMessage(tx, [address]);
    }

    private static async Task SeedTrackedAddressAsync(IDocumentStore store, string lifecycleStatus, bool readable, bool authoritative, bool degraded)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new TrackedAddressStatusDocument
        {
            Id = TrackedAddressStatusDocument.GetId(Address),
            EntityType = TrackedEntityType.Address,
            EntityId = Address,
            Address = Address,
            Tracked = true,
            LifecycleStatus = lifecycleStatus,
            Readable = readable,
            Authoritative = authoritative,
            Degraded = degraded
        });
        await session.SaveChangesAsync();
    }

    private static async Task SeedTrackedTokenAsync(IDocumentStore store, string lifecycleStatus, bool readable, bool authoritative, bool degraded)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new TrackedTokenStatusDocument
        {
            Id = TrackedTokenStatusDocument.GetId(TokenId),
            EntityType = TrackedEntityType.Token,
            EntityId = TokenId,
            TokenId = TokenId,
            Tracked = true,
            LifecycleStatus = lifecycleStatus,
            Readable = readable,
            Authoritative = authoritative,
            Degraded = degraded
        });
        await session.SaveChangesAsync();
    }

    private static async Task SeedMetaTransactionAsync(IDocumentStore store, string txId, string address, string tokenId)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new MetaTransaction
        {
            Id = txId,
            Addresses = [address],
            TokenIds = [tokenId],
            Inputs = [],
            Outputs = [],
            MissingTransactions = [],
            IllegalRoots = []
        });
        await session.SaveChangesAsync();
    }

    private static async Task SeedLifecycleProjectionAsync(IDocumentStore store, string txId, string lifecycleStatus, string? blockHash, int? blockHeight)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new TxLifecycleProjectionDocument
        {
            Id = TxLifecycleProjectionDocument.GetId(txId),
            TxId = txId,
            Known = true,
            LifecycleStatus = lifecycleStatus,
            Authoritative = lifecycleStatus == TxLifecycleStatus.Confirmed,
            RelevantToManagedScope = true,
            RelevanceTypes = ["address", "token"],
            SeenBySources = ["node"],
            SeenInMempool = lifecycleStatus == TxLifecycleStatus.SeenInMempool,
            BlockHash = blockHash,
            BlockHeight = blockHeight,
            LastObservedAt = DateTimeOffset.UtcNow
        });
        await session.SaveChangesAsync();
    }

    private sealed class FakeRealtimeEventDispatcher : IRealtimeEventDispatcher
    {
        public Dictionary<string, List<RealtimeEventResponse>> AddressEvents { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<RealtimeEventResponse>> TokenEvents { get; } = new(StringComparer.Ordinal);

        public Task SubscribeToAddressStream(string connectionId, string address) => Task.CompletedTask;

        public Task UnsubscribeToAddressStream(string connectionId, string address) => Task.CompletedTask;

        public Task SubscribeToTokenStream(string connectionId, string tokenId) => Task.CompletedTask;

        public Task UnsubscribeToTokenStream(string connectionId, string tokenId) => Task.CompletedTask;

        public Task PublishToAddressAsync(string address, RealtimeEventResponse eventDto)
        {
            if (!AddressEvents.TryGetValue(address, out var events))
            {
                events = [];
                AddressEvents[address] = events;
            }

            events.Add(eventDto);
            return Task.CompletedTask;
        }

        public Task PublishToTokenAsync(string tokenId, RealtimeEventResponse eventDto)
        {
            if (!TokenEvents.TryGetValue(tokenId, out var events))
            {
                events = [];
                TokenEvents[tokenId] = events;
            }

            events.Add(eventDto);
            return Task.CompletedTask;
        }
    }
}
