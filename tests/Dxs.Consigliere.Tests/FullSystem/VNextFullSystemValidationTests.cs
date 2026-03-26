using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Script;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Tokens;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Tests.Shared;

using Raven.Client.Documents;
using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.FullSystem;

public class VNextFullSystemValidationTests : RavenTestDriver
{
    private const string IssuerAddress = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    private const string ReceiverAddress = "1KFHE7w8BhaENAswwryaoccDb6qcT6DbYY";
    private const string TokenId = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string IssueTxId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string TransferTxId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string BlockHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string TransferBlockHash = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

    static VNextFullSystemValidationTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }

    [Fact]
    public async Task ReplayScenario_RebuildsTxAddressTokenAndReadinessSurfaces()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTrackedScopeAsync(store, TrackedEntityLifecycleStatus.Live, readable: true, authoritative: true, degraded: false);
        await SeedIssueAndTransferAsync(store);
        await AppendIssueAndPendingTransferAsync(store);

        var txRebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var addressRebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var tokenRebuilder = new TokenProjectionRebuilder(store, new RavenObservationJournalReader(store), addressRebuilder);

        await txRebuilder.RebuildAsync();
        await addressRebuilder.RebuildAsync();
        await tokenRebuilder.RebuildAsync();

        using var session = store.OpenAsyncSession();
        var issueState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(IssueTxId));
        var transferState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(TransferTxId));
        var receiverBsvBalance = await session.LoadAsync<AddressBalanceProjectionDocument>(
            AddressBalanceProjectionDocument.GetId(ReceiverAddress, null));
        var receiverTokenBalance = await session.LoadAsync<AddressBalanceProjectionDocument>(
            AddressBalanceProjectionDocument.GetId(ReceiverAddress, TokenId));
        var tokenState = await session.LoadAsync<TokenStateProjectionDocument>(TokenStateProjectionDocument.GetId(TokenId));
        var issueHistory = await session.LoadAsync<TokenHistoryProjectionDocument>(TokenHistoryProjectionDocument.GetId(TokenId, IssueTxId));
        var transferHistory = await session.LoadAsync<TokenHistoryProjectionDocument>(TokenHistoryProjectionDocument.GetId(TokenId, TransferTxId));
        var addressReadiness = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(IssuerAddress));
        var tokenReadiness = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(TokenId));

        Assert.NotNull(issueState);
        Assert.NotNull(transferState);
        Assert.True(issueState.Authoritative);
        Assert.Equal(TxLifecycleStatus.Confirmed, issueState.LifecycleStatus);
        Assert.False(transferState.Authoritative);
        Assert.Equal(TxLifecycleStatus.SeenInMempool, transferState.LifecycleStatus);
        Assert.NotNull(receiverBsvBalance);
        Assert.Equal(900, receiverBsvBalance.Satoshis);
        Assert.NotNull(receiverTokenBalance);
        Assert.Equal(50, receiverTokenBalance.Satoshis);
        Assert.NotNull(tokenState);
        Assert.Equal(TokenProjectionProtocolType.Stas, tokenState.ProtocolType);
        Assert.Equal(TokenProjectionValidationStatus.Valid, tokenState.ValidationStatus);
        Assert.Equal(50, tokenState.TotalKnownSupply);
        Assert.NotNull(issueHistory);
        Assert.NotNull(transferHistory);
        Assert.True(addressReadiness.Readable);
        Assert.True(tokenReadiness.Authoritative);

    }

    [Fact]
    public async Task ReorgScenario_RevertsBusinessStateAndEmitsTxReorged()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTrackedScopeAsync(store, TrackedEntityLifecycleStatus.Live, readable: true, authoritative: true, degraded: false);
        await SeedIssueAndTransferAsync(store);

        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var blockJournal = new RavenObservationJournal<BlockObservation>(store);
        await txJournal.AppendAsync(CreateTxObservation(TxObservationEventType.SeenInBlock, IssueTxId, 1, BlockHash, 100));
        await txJournal.AppendAsync(CreateTxObservation(TxObservationEventType.SeenInBlock, TransferTxId, 2, TransferBlockHash, 101));

        var txRebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var addressRebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var tokenRebuilder = new TokenProjectionRebuilder(store, new RavenObservationJournalReader(store), addressRebuilder);
        await txRebuilder.RebuildAsync();
        await addressRebuilder.RebuildAsync();
        await tokenRebuilder.RebuildAsync();

        await blockJournal.AppendAsync(CreateBlockObservation(BlockObservationEventType.Disconnected, TransferBlockHash, 3));
        await txRebuilder.RebuildAsync();
        await addressRebuilder.RebuildAsync();
        await tokenRebuilder.RebuildAsync();

        using var session = store.OpenAsyncSession();
        var transferState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(TransferTxId));
        var receiverBsv = await session.LoadAsync<AddressBalanceProjectionDocument>(AddressBalanceProjectionDocument.GetId(ReceiverAddress, null));
        var issuerToken = await session.LoadAsync<AddressBalanceProjectionDocument>(AddressBalanceProjectionDocument.GetId(IssuerAddress, TokenId));
        var receiverToken = await session.LoadAsync<AddressBalanceProjectionDocument>(AddressBalanceProjectionDocument.GetId(ReceiverAddress, TokenId));
        var tokenState = await session.LoadAsync<TokenStateProjectionDocument>(TokenStateProjectionDocument.GetId(TokenId));
        var issueHistory = await session.LoadAsync<TokenHistoryProjectionDocument>(TokenHistoryProjectionDocument.GetId(TokenId, IssueTxId));
        var transferHistory = await session.LoadAsync<TokenHistoryProjectionDocument>(TokenHistoryProjectionDocument.GetId(TokenId, TransferTxId));

        Assert.NotNull(transferState);
        Assert.Equal(TxLifecycleStatus.Reorged, transferState.LifecycleStatus);
        Assert.False(transferState.Authoritative);
        Assert.Null(receiverBsv);
        Assert.NotNull(issuerToken);
        Assert.Equal(50, issuerToken.Satoshis);
        Assert.Null(receiverToken);
        Assert.NotNull(tokenState);
        Assert.Equal(0, tokenState.TotalKnownSupply);
        Assert.NotNull(issueHistory);
        Assert.Equal(IssueTxId, issueHistory.TxId);
        Assert.Null(transferHistory);
    }

    [Fact]
    public async Task SoakScenario_RepeatedRebuildCyclesRemainDeterministic()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTrackedScopeAsync(store, TrackedEntityLifecycleStatus.Live, readable: true, authoritative: true, degraded: false);
        await SeedIssueAndTransferAsync(store);
        await AppendIssueAndPendingTransferAsync(store);

        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var duplicate = CreateTxObservation(TxObservationEventType.SeenInMempool, TransferTxId, 2);
        await txJournal.AppendAsync(duplicate);

        var txRebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var addressRebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var tokenRebuilder = new TokenProjectionRebuilder(store, new RavenObservationJournalReader(store), addressRebuilder);

        var txCheckpoint = default(ProjectionCheckpoint);
        var addressCheckpoint = default(ProjectionCheckpoint);
        var tokenCheckpoint = default(ProjectionCheckpoint);
        string? lifecycleStatus = null;
        long? supply = null;

        for (var i = 0; i < 3; i++)
        {
            txCheckpoint = await txRebuilder.RebuildAsync();
            addressCheckpoint = await addressRebuilder.RebuildAsync();
            tokenCheckpoint = await tokenRebuilder.RebuildAsync();

            using var session = store.OpenAsyncSession();
            var txState = await session.LoadAsync<TxLifecycleProjectionDocument>(TxLifecycleProjectionDocument.GetId(TransferTxId));
            var tokenState = await session.LoadAsync<TokenStateProjectionDocument>(TokenStateProjectionDocument.GetId(TokenId));

            Assert.NotNull(txState);
            Assert.NotNull(tokenState);

            if (lifecycleStatus is null)
            {
                lifecycleStatus = txState.LifecycleStatus;
                supply = tokenState.TotalKnownSupply;
            }
            else
            {
                Assert.Equal(lifecycleStatus, txState.LifecycleStatus);
                Assert.Equal(supply, tokenState.TotalKnownSupply);
            }
        }

        Assert.True(txCheckpoint.HasValue);
        Assert.True(addressCheckpoint.HasValue);
        Assert.True(tokenCheckpoint.HasValue);
        Assert.Equal(2, txCheckpoint.Sequence.Value);
        Assert.Equal(2, addressCheckpoint.Sequence.Value);
        Assert.Equal(2, tokenCheckpoint.Sequence.Value);
        Assert.Equal(TxLifecycleStatus.SeenInMempool, lifecycleStatus);
        Assert.Equal(50, supply);
    }

    private static async Task SeedTrackedScopeAsync(IDocumentStore store, string lifecycleStatus, bool readable, bool authoritative, bool degraded)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new TrackedAddressDocument
        {
            Id = TrackedAddressDocument.GetId(IssuerAddress),
            EntityType = TrackedEntityType.Address,
            EntityId = IssuerAddress,
            Address = IssuerAddress,
            Name = "issuer",
            Tracked = true,
            LifecycleStatus = lifecycleStatus,
            Readable = readable,
            Authoritative = authoritative,
            Degraded = degraded
        });
        await session.StoreAsync(new TrackedAddressStatusDocument
        {
            Id = TrackedAddressStatusDocument.GetId(IssuerAddress),
            EntityType = TrackedEntityType.Address,
            EntityId = IssuerAddress,
            Address = IssuerAddress,
            Tracked = true,
            LifecycleStatus = lifecycleStatus,
            Readable = readable,
            Authoritative = authoritative,
            Degraded = degraded
        });
        await session.StoreAsync(new TrackedTokenDocument
        {
            Id = TrackedTokenDocument.GetId(TokenId),
            EntityType = TrackedEntityType.Token,
            EntityId = TokenId,
            TokenId = TokenId,
            Symbol = "TEST",
            Tracked = true,
            LifecycleStatus = lifecycleStatus,
            Readable = readable,
            Authoritative = authoritative,
            Degraded = degraded
        });
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

    private static async Task SeedIssueAndTransferAsync(IDocumentStore store)
    {
        await SeedTransactionAsync(
            store,
            CreateTransaction(
                IssueTxId,
                outputs:
                [
                    CreateOutput(IssueTxId, 0, IssuerAddress, null, 1000, ScriptType.P2PKH),
                    CreateOutput(IssueTxId, 1, IssuerAddress, TokenId, 50, ScriptType.P2STAS)
                ],
                isIssue: true,
                isValidIssue: true,
                redeemAddress: IssuerAddress,
                timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_000)
            )
        );
        await SeedTransactionAsync(
            store,
            CreateTransaction(
                TransferTxId,
                inputs:
                [
                    CreateInput(IssueTxId, 0),
                    CreateInput(IssueTxId, 1)
                ],
                outputs:
                [
                    CreateOutput(TransferTxId, 0, ReceiverAddress, null, 900, ScriptType.P2PKH),
                    CreateOutput(TransferTxId, 1, ReceiverAddress, TokenId, 50, ScriptType.P2STAS)
                ],
                allStasInputsKnown: true,
                illegalRoots: [],
                timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_000_100)
            )
        );
    }

    private static async Task AppendIssueAndPendingTransferAsync(IDocumentStore store)
    {
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        await txJournal.AppendAsync(CreateTxObservation(TxObservationEventType.SeenInBlock, IssueTxId, 1, BlockHash, 100));
        await txJournal.AppendAsync(CreateTxObservation(TxObservationEventType.SeenInMempool, TransferTxId, 2));
    }

    private static ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> CreateTxObservation(
        string eventType,
        string txId,
        long second,
        string? blockHash = null,
        int? blockHeight = null)
    {
        var observation = new TxObservation(
            eventType,
            TxObservationSource.Node,
            txId,
            DateTimeOffset.FromUnixTimeSeconds(1_710_000_000 + second),
            blockHash,
            blockHeight,
            0);

        var fingerprint = eventType == TxObservationEventType.SeenInBlock
            ? $"node|{eventType}|{txId}|{blockHash}"
            : $"node|{eventType}|{txId}";

        return new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
            new ObservationJournalEntry<TxObservation>(observation),
            new DedupeFingerprint(fingerprint));
    }

    private static ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>> CreateBlockObservation(
        string eventType,
        string blockHash,
        long second)
        => new(
            new ObservationJournalEntry<BlockObservation>(
                new BlockObservation(
                    eventType,
                    TxObservationSource.Node,
                    blockHash,
                    DateTimeOffset.FromUnixTimeSeconds(1_710_000_000 + second))),
            new DedupeFingerprint($"node|{eventType}|{blockHash}"));

    private static MetaTransaction CreateTransaction(
        string txId,
        MetaTransaction.Input[]? inputs = null,
        MetaOutput[]? outputs = null,
        bool isIssue = false,
        bool isValidIssue = false,
        bool allStasInputsKnown = false,
        List<string>? illegalRoots = null,
        string? redeemAddress = null,
        DateTimeOffset? timestamp = null)
        => new()
        {
            Id = txId,
            Inputs = inputs ?? [],
            Outputs = outputs?.Select(x => new MetaTransaction.Output(x)).ToList() ?? [],
            Addresses = outputs?.Select(x => x.Address).Where(x => x != null).Distinct().ToList() ?? [],
            TokenIds = outputs?.Select(x => x.TokenId).Where(x => x != null).Distinct().ToList() ?? [],
            IsStas = outputs?.Any(x => x.Type is ScriptType.P2STAS or ScriptType.DSTAS) == true,
            IsIssue = isIssue,
            IsValidIssue = isValidIssue,
            AllStasInputsKnown = allStasInputsKnown,
            IllegalRoots = illegalRoots ?? [],
            MissingTransactions = [],
            RedeemAddress = redeemAddress,
            Timestamp = (timestamp ?? DateTimeOffset.UnixEpoch).ToUnixTimeSeconds()
        };

    private static MetaTransaction.Input CreateInput(string txId, int vout)
        => new()
        {
            Id = MetaOutput.GetId(txId, vout),
            TxId = txId,
            Vout = vout
        };

    private static MetaOutput CreateOutput(string txId, int vout, string address, string? tokenId, long satoshis, ScriptType type)
        => new()
        {
            Id = MetaOutput.GetId(txId, vout),
            TxId = txId,
            Vout = vout,
            Address = address,
            TokenId = tokenId,
            Satoshis = satoshis,
            Type = type,
            ScriptPubKey = $"script-{txId}-{vout}",
            Spent = false
        };

    private static async Task SeedTransactionAsync(IDocumentStore store, MetaTransaction transaction)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(transaction, transaction.Id);

        foreach (var output in transaction.Outputs)
        {
            await session.StoreAsync(new MetaOutput
            {
                Id = output.Id,
                TxId = transaction.Id,
                Vout = int.Parse(output.Id!.Split(':')[1]),
                Address = output.Address,
                TokenId = output.TokenId,
                Satoshis = output.Satoshis,
                Type = output.Type,
                ScriptPubKey = $"script-{transaction.Id}-{int.Parse(output.Id!.Split(':')[1])}",
                Spent = false
            }, output.Id);
        }

        await session.SaveChangesAsync();
    }

}
