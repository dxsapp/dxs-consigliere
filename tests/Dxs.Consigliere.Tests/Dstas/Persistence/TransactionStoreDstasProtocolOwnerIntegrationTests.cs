using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Tokens;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Common.Journal;
using Dxs.Tests.Shared;

using MediatR;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Raven.Client.Documents;
using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TransactionStoreDstasProtocolOwnerIntegrationTests : RavenTestDriver
{
    private const string OwnerChainId = "owner_multisig_positive_spend";
    private const long Timestamp = 1_710_002_000;

    static TransactionStoreDstasProtocolOwnerIntegrationTests()
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
    public async Task SaveTransaction_PersistsOwnerMultisigOutputAndPositiveSpendSemantics()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = BuildStore(store);
        var chain = DstasProtocolOwnerFixture.LoadChain(OwnerChainId);
        var toOwner = chain.Transactions.Single(x => x.Label == "to_owner_multisig");

        await SeedParentTransactionsAsync(store, toOwner);

        var transaction = Transaction.Parse(toOwner.TxHex, Network.Mainnet);
        await sut.SaveTransaction(transaction, Timestamp, null);
        await sut.UpdateStasAttributes(transaction.Id);

        using var session = store.OpenAsyncSession();
        var saved = await session.LoadAsync<MetaTransaction>(transaction.Id);
        var ownerOutput = await session.LoadAsync<MetaOutput>(MetaOutput.GetId(transaction.Id, 0));

        Assert.NotNull(saved);
        Assert.NotNull(ownerOutput);
        Assert.True(saved!.IsStas);
        Assert.Equal(1, saved.DstasSpendingType);
        Assert.Null(saved.DstasEventType);
        Assert.True(saved.AllStasInputsKnown);
        Assert.NotNull(ownerOutput!.Address);
        Assert.NotNull(ownerOutput.Hash160);
        Assert.Equal(ScriptType.DSTAS, ownerOutput.Type);
        Assert.NotNull(ownerOutput.TokenId);
        Assert.True(ownerOutput.DstasFreezeEnabled);
        Assert.True(ownerOutput.DstasConfiscationEnabled);
        Assert.Equal(2, ownerOutput.DstasServiceFields.Length);
        Assert.Equal("empty", ownerOutput.DstasActionType);

        var reader = LockingScriptReader.Read(ownerOutput.ScriptPubKey, Network.Mainnet);
        Assert.NotNull(reader.Dstas);
        Assert.Equal(20, reader.Dstas!.Owner.Length);
        Assert.NotNull(reader.Address);
    }

    [Fact]
    public async Task TokenProjection_ReplaysOwnerMultisigPositiveSpendAcrossTokenState()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = BuildStore(store);
        var chain = DstasProtocolOwnerFixture.LoadChain(OwnerChainId);
        var issue = chain.Transactions.Single(x => x.Label == "issue");
        var toOwner = chain.Transactions.Single(x => x.Label == "to_owner_multisig");
        var spend = chain.Transactions.Single(x => x.Label == "owner_multisig_spend");

        await SeedParentTransactionsAsync(store, issue);

        foreach (var fixture in new[] { issue, toOwner, spend })
        {
            var tx = Transaction.Parse(fixture.TxHex, Network.Mainnet);
            await sut.SaveTransaction(tx, Timestamp, null);
            await sut.UpdateStasAttributes(tx.Id);
        }

        var tokenId = Transaction.Parse(issue.TxHex, Network.Mainnet).Outputs.Single(x => x.Type == ScriptType.DSTAS).TokenId!;
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        await txJournal.AppendAsync(CreateObservation(TxObservationEventType.SeenInBlock, issue, 1, "block-issue", 100));
        await txJournal.AppendAsync(CreateObservation(TxObservationEventType.SeenInBlock, toOwner, 2, "block-owner", 101));
        await txJournal.AppendAsync(CreateObservation(TxObservationEventType.SeenInBlock, spend, 3, "block-spend", 102));

        var addressRebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var tokenRebuilder = new TokenProjectionRebuilder(store, new RavenObservationJournalReader(store), addressRebuilder);
        var tokenReader = new TokenProjectionReader(store);

        await tokenRebuilder.RebuildAsync();

        var state = await tokenReader.LoadStateAsync(tokenId);
        var history = await tokenReader.LoadHistoryAsync(tokenId);

        Assert.NotNull(state);
        Assert.Equal(TokenProjectionProtocolType.Dstas, state!.ProtocolType);
        Assert.Equal(TokenProjectionValidationStatus.Valid, state.ValidationStatus);
        Assert.Equal(100, state.TotalKnownSupply);
        Assert.Equal(3, history.Count);
        Assert.Contains(history, x => x.TxId == Transaction.Parse(toOwner.TxHex, Network.Mainnet).Id);
        Assert.Contains(history, x => x.TxId == Transaction.Parse(spend.TxHex, Network.Mainnet).Id);

        using var session = store.OpenAsyncSession();
        var storedOwner = await session.LoadAsync<MetaOutput>(MetaOutput.GetId(Transaction.Parse(toOwner.TxHex, Network.Mainnet).Id, 0));
        Assert.NotNull(storedOwner);
        Assert.NotNull(storedOwner!.Address);
        Assert.NotNull(storedOwner.Hash160);
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

    private static ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> CreateObservation(
        string eventType,
        DstasProtocolTransactionFixture fixture,
        long sequence,
        string? blockHash,
        int? blockHeight)
    {
        var txId = Transaction.Parse(fixture.TxHex, Network.Mainnet).Id;
        var observation = new TxObservation(
            eventType,
            "sdk-fixture",
            txId,
            DateTimeOffset.UnixEpoch.AddSeconds(sequence),
            blockHash,
            blockHeight,
            0);

        var fingerprint = eventType == TxObservationEventType.SeenInBlock
            ? $"sdk-fixture|{eventType}|{txId}|{blockHash}"
            : $"sdk-fixture|{eventType}|{txId}";

        return new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
            new ObservationJournalEntry<TxObservation>(observation),
            new DedupeFingerprint(fingerprint));
    }

    private static async Task SeedParentTransactionsAsync(IDocumentStore store, DstasProtocolTransactionFixture fixture)
    {
        var parents = fixture.Prevouts
            .GroupBy(x => x.TxId, StringComparer.Ordinal)
            .Select(group => BuildParentTransaction(group.Key, group))
            .ToArray();

        using var session = store.OpenAsyncSession();
        foreach (var parent in parents)
            await session.StoreAsync(parent, parent.Id);

        await session.SaveChangesAsync();
    }

    private static MetaTransaction BuildParentTransaction(string txId, IEnumerable<DstasProtocolPrevoutFixture> prevouts)
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

    private static MetaTransaction.Output BuildParentOutput(DstasProtocolPrevoutFixture prevout)
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
