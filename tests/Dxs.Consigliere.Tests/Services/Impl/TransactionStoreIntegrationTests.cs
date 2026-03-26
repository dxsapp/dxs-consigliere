using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using MediatR;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TransactionStoreIntegrationTests : RavenTestDriver
{
    private const string SampleTransactionHex = "0100000001c6f4b6176d3f4d6c6d9e198ba89a4eb7a1b08e6a705cc8cf0f8f2f3e3bcedf1f000000006b4830450221009af2d63b8ef3ebf8c7a227327d8e1a89f5929087566bbb6d6f74a09a87e2375d022007f8cefa32f6d829bb3f8792dd11e5d8f1cb4e4f4f84f7a8d431fed0b8ff103a4121022b698a0f0a1f1fb43fb8f33c2d72cbe7f3f8d98ef1a304681140f64e5681970fffffffff02e8030000000000001976a91489abcdefabbaabbaabbaabbaabbaabbaabbaabba88ac0000000000000000066a040102030400000000";

    static TransactionStoreIntegrationTests()
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
    public async Task UpdateStasAttributes_SetsFreezeEventAndContinuity()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = BuildStore(store);

        const string parentId = "1111111111111111111111111111111111111111111111111111111111111111";
        const string txId = "2222222222222222222222222222222222222222222222222222222222222222";

        await SeedTransaction(store, parentId, new RawMetaTransaction
        {
            Inputs = [],
            Outputs =
            [
                new RawOutput
                {
                    Type = "DSTAS",
                    TokenId = "token-1",
                    Hash160 = "issuer-1",
                    Address = "1ParentAddress",
                    DstasFrozen = false,
                    DstasActionType = "empty",
                    DstasOptionalDataFingerprint = "opt-1"
                }
            ],
            MissingTransactions = [],
            IllegalRoots = [],
            IsIssue = false,
            IsValidIssue = false
        });

        await SeedTransaction(store, txId, new RawMetaTransaction
        {
            Inputs =
            [
                new RawInput
                {
                    TxId = parentId,
                    Vout = 0,
                    DstasSpendingType = 2
                }
            ],
            Outputs =
            [
                new RawOutput
                {
                    Type = "DSTAS",
                    TokenId = "token-1",
                    Hash160 = "receiver-1",
                    Address = "1ReceiverAddress",
                    DstasFrozen = true,
                    DstasActionType = "freeze",
                    DstasOptionalDataFingerprint = "opt-1"
                }
            ],
            MissingTransactions = [],
            IllegalRoots = [],
            IsIssue = false,
            IsValidIssue = false
        });

        await sut.UpdateStasAttributes(txId);

        using var session = store.OpenAsyncSession();
        var updated = await session.LoadAsync<MetaTransaction>(txId);

        Assert.NotNull(updated);
        Assert.True(updated!.IsStas);
        Assert.Equal("freeze", updated.DstasEventType);
        Assert.Equal(2, updated.DstasSpendingType);
        Assert.False(updated.DstasInputFrozen);
        Assert.True(updated.DstasOutputFrozen);
        Assert.True(updated.DstasOptionalDataContinuity);
    }

    [Fact]
    public async Task UpdateStasAttributes_BlocksRedeemWhenInputIsFrozen()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = BuildStore(store);

        const string parentId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string txId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string tokenId = "issuer-token-hash";

        await SeedTransaction(store, parentId, new RawMetaTransaction
        {
            Inputs = [],
            Outputs =
            [
                new RawOutput
                {
                    Type = "DSTAS",
                    TokenId = tokenId,
                    Hash160 = tokenId,
                    Address = "1FrozenOwner",
                    DstasFrozen = true,
                    DstasActionType = "freeze"
                }
            ],
            MissingTransactions = [],
            IllegalRoots = [],
            IsIssue = false,
            IsValidIssue = false
        });

        await SeedTransaction(store, txId, new RawMetaTransaction
        {
            Inputs =
            [
                new RawInput
                {
                    TxId = parentId,
                    Vout = 0,
                    DstasSpendingType = 1
                }
            ],
            Outputs =
            [
                new RawOutput
                {
                    Type = "P2PKH",
                    Hash160 = tokenId,
                    Address = "1IssuerRedeem"
                }
            ],
            MissingTransactions = [],
            IllegalRoots = [],
            IsIssue = false,
            IsValidIssue = false
        });

        await sut.UpdateStasAttributes(txId);

        using var session = store.OpenAsyncSession();
        var updated = await session.LoadAsync<MetaTransaction>(txId);

        Assert.NotNull(updated);
        Assert.True(updated!.IsStas);
        Assert.False(updated.IsRedeem);
        Assert.Equal(1, updated.DstasSpendingType);
        Assert.Null(updated.DstasEventType);
    }

    [Fact]
    public async Task UpdateStasAttributes_MapsSwapEventFromSwapMarkedInput()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = BuildStore(store);

        const string parentId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        const string txId = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

        await SeedTransaction(store, parentId, new RawMetaTransaction
        {
            Inputs = [],
            Outputs =
            [
                new RawOutput
                {
                    Type = "DSTAS",
                    TokenId = "token-3",
                    Hash160 = "issuer-3",
                    Address = "1SwapOwner",
                    DstasFrozen = false,
                    DstasActionType = "swap",
                    DstasOptionalDataFingerprint = "opt-swap"
                }
            ],
            MissingTransactions = [],
            IllegalRoots = [],
            IsIssue = false,
            IsValidIssue = false
        });

        await SeedTransaction(store, txId, new RawMetaTransaction
        {
            Inputs =
            [
                new RawInput
                {
                    TxId = parentId,
                    Vout = 0,
                    DstasSpendingType = 1
                }
            ],
            Outputs =
            [
                new RawOutput
                {
                    Type = "DSTAS",
                    TokenId = "token-3",
                    Hash160 = "counterparty-3",
                    Address = "1Counterparty",
                    DstasFrozen = false,
                    DstasActionType = "empty",
                    DstasOptionalDataFingerprint = "opt-swap"
                }
            ],
            MissingTransactions = [],
            IllegalRoots = [],
            IsIssue = false,
            IsValidIssue = false
        });

        await sut.UpdateStasAttributes(txId);

        using var session = store.OpenAsyncSession();
        var updated = await session.LoadAsync<MetaTransaction>(txId);

        Assert.NotNull(updated);
        Assert.True(updated!.IsStas);
        Assert.Equal("swap", updated.DstasEventType);
        Assert.Equal(1, updated.DstasSpendingType);
        Assert.True(updated.DstasOptionalDataContinuity);
    }

    [Fact]
    public async Task UpdateStasAttributes_RequiresCurrentOwnerToMatchIssuerForRedeem()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = BuildStore(store);

        const string parentId = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        const string txId = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
        const string tokenId = "issuer-token-hash";

        await SeedTransaction(store, parentId, new RawMetaTransaction
        {
            Inputs = [],
            Outputs =
            [
                new RawOutput
                {
                    Type = "DSTAS",
                    TokenId = tokenId,
                    Hash160 = tokenId,
                    Address = "1DifferentCurrentOwner",
                    DstasFrozen = false,
                    DstasActionType = "empty"
                }
            ],
            MissingTransactions = [],
            IllegalRoots = [],
            IsIssue = false,
            IsValidIssue = false
        });

        await SeedTransaction(store, txId, new RawMetaTransaction
        {
            Inputs =
            [
                new RawInput
                {
                    TxId = parentId,
                    Vout = 0,
                    DstasSpendingType = 1
                }
            ],
            Outputs =
            [
                new RawOutput
                {
                    Type = "P2PKH",
                    Hash160 = tokenId,
                    Address = "1IssuerRedeem"
                }
            ],
            MissingTransactions = [],
            IllegalRoots = [],
            IsIssue = false,
            IsValidIssue = false
        });

        await sut.UpdateStasAttributes(txId);

        using var session = store.OpenAsyncSession();
        var updated = await session.LoadAsync<MetaTransaction>(txId);

        Assert.NotNull(updated);
        Assert.True(updated!.IsStas);
        Assert.False(updated.IsRedeem);
        Assert.Equal(1, updated.DstasSpendingType);
        Assert.Null(updated.DstasEventType);
    }

    [Fact]
    public async Task UpdateStasAttributes_RecognizesRedeemWhenIssuerIsCurrentOwner()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = BuildStore(store);

        const string parentId = "1212121212121212121212121212121212121212121212121212121212121212";
        const string txId = "3434343434343434343434343434343434343434343434343434343434343434";
        const string tokenId = "issuer-token-hash";
        const string issuerAddress = "1IssuerRedeem";

        await SeedTransaction(store, parentId, new RawMetaTransaction
        {
            Inputs = [],
            Outputs =
            [
                new RawOutput
                {
                    Type = "DSTAS",
                    TokenId = tokenId,
                    Hash160 = tokenId,
                    Address = issuerAddress,
                    DstasFrozen = false,
                    DstasActionType = "empty"
                }
            ],
            MissingTransactions = [],
            IllegalRoots = [],
            IsIssue = false,
            IsValidIssue = false
        });

        await SeedTransaction(store, txId, new RawMetaTransaction
        {
            Inputs =
            [
                new RawInput
                {
                    TxId = parentId,
                    Vout = 0,
                    DstasSpendingType = 1
                }
            ],
            Outputs =
            [
                new RawOutput
                {
                    Type = "P2PKH",
                    Hash160 = tokenId,
                    Address = issuerAddress
                }
            ],
            MissingTransactions = [],
            IllegalRoots = [],
            IsIssue = false,
            IsValidIssue = false
        });

        await sut.UpdateStasAttributes(txId);

        using var session = store.OpenAsyncSession();
        var updated = await session.LoadAsync<MetaTransaction>(txId);

        Assert.NotNull(updated);
        Assert.True(updated!.IsStas);
        Assert.True(updated.IsRedeem);
        Assert.Equal(1, updated.DstasSpendingType);
        Assert.Null(updated.DstasEventType);
        Assert.Equal(issuerAddress, updated.RedeemAddress);
    }

    [Fact]
    public async Task SaveTransaction_PersistsRawMetaAndPrevoutSpendState()
    {
        using var store = GetDocumentStore();
        var sut = BuildStore(store);
        var transaction = Transaction.Parse(SampleTransactionHex, Network.Mainnet);
        var prevoutId = MetaOutput.GetId(transaction.Inputs[0].TxId, transaction.Inputs[0].Vout);

        using (var session = store.OpenAsyncSession())
        {
            await session.StoreAsync(new MetaOutput
            {
                Id = prevoutId,
                TxId = transaction.Inputs[0].TxId,
                Vout = (int)transaction.Inputs[0].Vout,
                Type = Dxs.Bsv.Script.ScriptType.P2PKH,
                Address = "1PrevoutOwner",
                Satoshis = 1000,
                Spent = false
            });
            await session.SaveChangesAsync();
        }

        var firstStatus = await sut.SaveTransaction(transaction, 1_710_000_000, null);
        var secondStatus = await sut.SaveTransaction(transaction, 1_710_000_010, null, "block-hash", 100, 2);

        Assert.Equal(TransactionProcessStatus.FoundInMempool, firstStatus);
        Assert.Equal(TransactionProcessStatus.UpdatedOnBlockConnected, secondStatus);

        using var verifySession = store.OpenAsyncSession();
        var raw = await verifySession.LoadAsync<TransactionHexData>(TransactionHexData.GetId(transaction.Id));
        var meta = await verifySession.LoadAsync<MetaTransaction>(transaction.Id);
        var prevout = await verifySession.LoadAsync<MetaOutput>(prevoutId);
        var createdOutput = await verifySession.LoadAsync<MetaOutput>(MetaOutput.GetId(transaction.Id, 0));

        Assert.NotNull(raw);
        Assert.Equal(transaction.Hex, raw!.Hex);
        Assert.NotNull(meta);
        Assert.Equal("block-hash", meta!.Block);
        Assert.Equal(100, meta.Height);
        Assert.Equal(2, meta.Index);
        Assert.Single(meta.Inputs);
        Assert.Equal(2, meta.Outputs.Count);
        Assert.NotNull(prevout);
        Assert.True(prevout!.Spent);
        Assert.Equal(transaction.Id, prevout.SpendTxId);
        Assert.NotNull(createdOutput);
        Assert.Equal(transaction.Id, createdOutput!.TxId);
    }

    [Fact]
    public async Task TryRemoveTransaction_DeletesTrackedDocumentsAndFreesSpentPrevout()
    {
        using var store = GetDocumentStore();
        var sut = BuildStore(store);
        var transaction = Transaction.Parse(SampleTransactionHex, Network.Mainnet);
        var prevoutId = MetaOutput.GetId(transaction.Inputs[0].TxId, transaction.Inputs[0].Vout);

        using (var session = store.OpenAsyncSession())
        {
            await session.StoreAsync(new MetaOutput
            {
                Id = prevoutId,
                TxId = transaction.Inputs[0].TxId,
                Vout = (int)transaction.Inputs[0].Vout,
                Type = Dxs.Bsv.Script.ScriptType.P2PKH,
                Address = "1PrevoutOwner",
                Satoshis = 1000,
                Spent = false
            });
            await session.SaveChangesAsync();
        }

        await sut.SaveTransaction(transaction, 1_710_000_000, null);

        var removed = await sut.TryRemoveTransaction(transaction.Id);

        Assert.NotNull(removed);
        Assert.Equal(transaction.Id, removed!.Id);

        using var verifySession = store.OpenAsyncSession();
        var raw = await verifySession.LoadAsync<TransactionHexData>(TransactionHexData.GetId(transaction.Id));
        var meta = await verifySession.LoadAsync<MetaTransaction>(transaction.Id);
        var prevout = await verifySession.LoadAsync<MetaOutput>(prevoutId);
        var deleted = await verifySession.LoadAsync<DeletedTransaction>(DeletedTransaction.GetId(transaction.Id));

        Assert.Null(raw);
        Assert.Null(meta);
        Assert.NotNull(prevout);
        Assert.False(prevout!.Spent);
        Assert.Null(prevout.SpendTxId);
        Assert.NotNull(deleted);
        Assert.Equal(transaction.Id, deleted!.MetaTransaction.Id);
        Assert.Equal(transaction.Hex, deleted.RawData);
    }

    [Fact]
    public async Task SaveTransaction_WhenObservedWithSameState_ReturnsNotModified()
    {
        using var store = GetDocumentStore();
        var sut = BuildStore(store);
        var transaction = Transaction.Parse(SampleTransactionHex, Network.Mainnet);

        await sut.SaveTransaction(transaction, 1_710_000_000, null, "block-hash", 100, 2);

        var status = await sut.SaveTransaction(transaction, 1_710_000_100, null, "block-hash", 100, 2);

        Assert.Equal(TransactionProcessStatus.NotModified, status);
    }

    private static TransactionStore BuildStore(Raven.Client.Documents.IDocumentStore store)
    {
        var networkProvider = new Mock<INetworkProvider>();
        networkProvider.SetupGet(x => x.Network).Returns(Network.Mainnet);

        return new TransactionStore(
            store,
            Mock.Of<IPublisher>(),
            Options.Create(new TransactionFilterConfig()),
            networkProvider.Object,
            new NullLogger<TransactionStore>()
        );
    }

    private static async Task SeedTransaction(Raven.Client.Documents.IDocumentStore store, string id, RawMetaTransaction tx)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(tx, id);
        await session.SaveChangesAsync();
    }

    private sealed class RawMetaTransaction
    {
        public List<RawInput> Inputs { get; set; } = [];
        public List<RawOutput> Outputs { get; set; } = [];
        public List<string> MissingTransactions { get; set; } = [];
        public List<string> IllegalRoots { get; set; } = [];
        public bool IsIssue { get; set; }
        public bool IsValidIssue { get; set; }
    }

    private sealed class RawInput
    {
        public string TxId { get; set; } = string.Empty;
        public int Vout { get; set; }
        public int? DstasSpendingType { get; set; }
    }

    private sealed class RawOutput
    {
        public string Type { get; set; } = "Unknown";
        public string? TokenId { get; set; }
        public string? Hash160 { get; set; }
        public string? Address { get; set; }
        public bool? DstasFrozen { get; set; }
        public string? DstasActionType { get; set; }
        public string? DstasOptionalDataFingerprint { get; set; }
    }
}
