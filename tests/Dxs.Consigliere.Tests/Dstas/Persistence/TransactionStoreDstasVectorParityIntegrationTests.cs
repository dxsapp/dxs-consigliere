using Dxs.Bsv;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using MediatR;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Raven.Client.Documents;
using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TransactionStoreDstasVectorParityIntegrationTests : RavenTestDriver
{
    private const long Timestamp = 1_710_001_000;

    static TransactionStoreDstasVectorParityIntegrationTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }

    public static IEnumerable<object?[]> VectorCases()
        => DstasConformanceVectorFixture.LoadExpectations()
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new object?[] { x.Key, x.Value.SpendingType, x.Value.EventType, x.Value.IsRedeem });

    [Theory]
    [MemberData(nameof(VectorCases))]
    public async Task UpdateStasAttributes_MatchesSharedConformanceVectors(
        string vectorId,
        int expectedSpendingType,
        string? expectedEventType,
        bool expectedIsRedeem)
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = BuildStore(store);
        var vector = DstasConformanceVectorFixture.Load(vectorId);

        await SeedParentTransactionsAsync(store, vector);

        var transaction = Transaction.Parse(vector.TxHex, Network.Mainnet);
        await sut.SaveTransaction(transaction, Timestamp, null);
        await sut.UpdateStasAttributes(transaction.Id);

        using var session = store.OpenAsyncSession();
        var updated = await session.LoadAsync<MetaTransaction>(transaction.Id);
        var savedHex = await session.LoadAsync<TransactionHexData>(TransactionHexData.GetId(transaction.Id));

        Assert.NotNull(updated);
        Assert.NotNull(savedHex);
        Assert.Equal(transaction.Hex, savedHex!.Hex);
        Assert.True(updated!.IsStas);
        Assert.True(updated.AllStasInputsKnown);
        Assert.Equal(expectedSpendingType, updated.DstasSpendingType);
        Assert.Equal(expectedEventType, updated.DstasEventType);
        Assert.Equal(expectedIsRedeem, updated.IsRedeem);

        var expectedTokenIds = transaction.Outputs
            .Where(x => x.Type is ScriptType.P2STAS or ScriptType.DSTAS)
            .Select(x => x.TokenId)
            .Concat(vector.Prevouts
                .Select(x => LockingScriptReader.Read(x.LockingScriptHex, Network.Mainnet))
                .Where(x => x.ScriptType is ScriptType.P2STAS or ScriptType.DSTAS)
                .Select(x => x.GetTokenId()))
            .Where(x => x is not null)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedTokenIds, updated.TokenIds.OrderBy(x => x, StringComparer.Ordinal));

        if (vectorId == "swap_cancel_valid")
        {
            var dstasOutput = updated.Outputs.First(x => x.Type == ScriptType.DSTAS);
            Assert.Equal("swap", dstasOutput.DstasActionType);
            Assert.False(string.IsNullOrWhiteSpace(dstasOutput.DstasRequestedScriptHash));
        }
    }

    private static TransactionStore BuildStore(IDocumentStore store)
    {
        var networkProvider = new Mock<INetworkProvider>();
        networkProvider.SetupGet(x => x.Network).Returns(Network.Mainnet);

        return new TransactionStore(
            store,
            Mock.Of<IPublisher>(),
            Options.Create(new TransactionFilterConfig()),
            networkProvider.Object,
            new NullLogger<TransactionStore>());
    }

    private static async Task SeedParentTransactionsAsync(IDocumentStore store, DstasConformanceVector vector)
    {
        var parents = vector.Prevouts
            .GroupBy(x => x.TxId, StringComparer.Ordinal)
            .Select(group => BuildParentTransaction(group.Key, group))
            .ToArray();

        using var session = store.OpenAsyncSession();
        foreach (var parent in parents)
            await session.StoreAsync(parent, parent.Id);

        await session.SaveChangesAsync();
    }

    private static MetaTransaction BuildParentTransaction(string txId, IEnumerable<DstasConformancePrevout> prevouts)
    {
        var prevoutsByVout = prevouts.ToDictionary(x => x.Vout);
        var maxVout = prevoutsByVout.Keys.Max();
        var outputs = new List<MetaTransaction.Output>(capacity: maxVout + 1);

        for (var vout = 0; vout <= maxVout; vout++)
        {
            outputs.Add(prevoutsByVout.TryGetValue(vout, out var prevout)
                ? BuildParentOutput(prevout)
                : new MetaTransaction.Output
                {
                    Id = MetaOutput.GetId(txId, vout),
                    Type = ScriptType.Unknown,
                    Satoshis = 0
                });
        }

        return new MetaTransaction
        {
            Id = txId,
            Block = null,
            Height = MetaTransaction.DefaultHeight,
            Index = 0,
            Timestamp = Timestamp - 1,
            Inputs = [],
            Outputs = outputs,
            Addresses = outputs.Where(x => x.Address is not null).Select(x => x.Address!).Distinct(StringComparer.Ordinal).ToList(),
            TokenIds = outputs.Where(x => x.TokenId is not null).Select(x => x.TokenId!).Distinct(StringComparer.Ordinal).ToList(),
            IsStas = outputs.Any(x => x.Type is ScriptType.P2STAS or ScriptType.DSTAS),
            IsIssue = false,
            IsValidIssue = false,
            IsRedeem = false,
            IsWithFee = false,
            IsWithNote = false,
            IllegalRoots = [],
            MissingTransactions = [],
            AllStasInputsKnown = true,
            RedeemAddress = null,
            StasFrom = null,
            Note = null,
            DstasEventType = null,
            DstasSpendingType = null,
            DstasInputFrozen = null,
            DstasOutputFrozen = null,
            DstasOptionalDataContinuity = null,
        };
    }

    private static MetaTransaction.Output BuildParentOutput(DstasConformancePrevout prevout)
    {
        var reader = LockingScriptReader.Read(prevout.LockingScriptHex, Network.Mainnet);
        var dstas = reader.Dstas;
        var serviceFields = dstas?.ServiceFields.Select(x => x.ToHexString()).ToArray();
        var optionalData = dstas?.OptionalData.Select(x => x.ToHexString()).ToArray();

        return new MetaTransaction.Output
        {
            Id = MetaOutput.GetId(prevout.TxId, prevout.Vout),
            Type = reader.ScriptType,
            Satoshis = prevout.Satoshis,
            Address = reader.Address?.Value,
            TokenId = reader.GetTokenId(),
            Hash160 = reader.Address?.Hash160.ToHexString(),
            DstasFlags = dstas?.Flags?.ToHexString(),
            DstasFreezeEnabled = dstas?.FreezeEnabled,
            DstasConfiscationEnabled = dstas?.ConfiscationEnabled,
            DstasFrozen = dstas?.Frozen,
            DstasFreezeAuthority = serviceFields != null && dstas?.FreezeEnabled == true && serviceFields.Length > 0 ? serviceFields[0] : null,
            DstasConfiscationAuthority = serviceFields != null && dstas?.ConfiscationEnabled == true
                ? serviceFields[dstas.FreezeEnabled ? 1 : 0]
                : null,
            DstasServiceFields = serviceFields,
            DstasActionType = dstas?.ActionType,
            DstasActionData = dstas?.ActionDataRaw is { Length: > 0 } ? dstas.ActionDataRaw.ToHexString() : null,
            DstasRequestedScriptHash = dstas?.RequestedScriptHash?.ToHexString(),
            DstasOptionalData = optionalData,
            DstasOptionalDataFingerprint = optionalData is { Length: > 0 } ? string.Join("|", optionalData) : null,
        };
    }
}
