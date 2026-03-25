using Dxs.Bsv;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using MediatR;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TransactionStoreIntegrationTests : RavenTestDriver
{
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
