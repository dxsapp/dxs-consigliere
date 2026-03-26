using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Script;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Tokens;
using Dxs.Tests.Shared;

using Raven.Client.Documents;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Tokens;

public class TokenProjectionRebuilderIntegrationTests : RavenTestDriver
{
    private const string TokenId = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string RedeemAddress = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    private const string HolderA = "1BoatSLRHtKNngkdXEeobR76b53LETtpyT";
    private const string HolderB = "1KFHE7w8BhaENAswwryaoccDb6qcT6DbYY";

    [Fact]
    public async Task RebuildAsync_ProjectsTokenStateHistoryAndUtxos()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTransactionAsync(
            store,
            CreateTokenTransaction(
                "tx-issue",
                outputs:
                [CreateOutput("tx-issue", 0, HolderA, TokenId, 50, ScriptType.P2STAS)],
                isIssue: true,
                isValidIssue: true,
                redeemAddress: RedeemAddress
            )
        );
        await SeedTransactionAsync(
            store,
            CreateTokenTransaction(
                "tx-transfer",
                inputs:
                [CreateInput("tx-issue", 0)],
                outputs:
                [CreateOutput("tx-transfer", 0, HolderB, TokenId, 50, ScriptType.P2STAS)],
                allStasInputsKnown: true,
                illegalRoots: []
            )
        );

        var txJournal = new RavenObservationJournal<TxObservation>(store);
        await txJournal.AppendAsync(CreateObservation(TxObservationEventType.SeenInBlock, "tx-issue", 1, "block-1", 100));
        await txJournal.AppendAsync(CreateObservation(TxObservationEventType.SeenInMempool, "tx-transfer", 2));

        var rebuilder = new TokenProjectionRebuilder(
            store,
            new RavenObservationJournalReader(store),
            new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store))
        );
        var reader = new TokenProjectionReader(store);

        var checkpoint = await rebuilder.RebuildAsync();
        var state = await reader.LoadStateAsync(TokenId);
        var history = await reader.LoadHistoryAsync(TokenId);
        var utxos = await reader.LoadUtxosAsync(TokenId);

        Assert.Equal(2, checkpoint.Sequence.Value);
        Assert.NotNull(state);
        Assert.Equal(TokenProjectionProtocolType.Stas, state.ProtocolType);
        Assert.True(state.IssuanceKnown);
        Assert.Equal(TokenProjectionValidationStatus.Valid, state.ValidationStatus);
        Assert.Equal(RedeemAddress, state.Issuer);
        Assert.Equal(50, state.TotalKnownSupply);
        Assert.Single(utxos);
        Assert.Equal("tx-transfer", utxos[0].TxId);
        Assert.Equal(2, history.Count);
        Assert.Contains(history, x => x.TxId == "tx-issue" && x.ConfirmedBlockHash == "block-1");
        Assert.Contains(history, x => x.TxId == "tx-transfer" && x.ConfirmedBlockHash == null);
    }

    [Fact]
    public async Task RebuildAsync_RemovesTokenHistoryAndRestoresStateAfterDroppedTransferDeletion()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTransactionAsync(
            store,
            CreateTokenTransaction(
                "tx-issue",
                outputs:
                [CreateOutput("tx-issue", 0, HolderA, TokenId, 50, ScriptType.P2STAS)],
                isIssue: true,
                isValidIssue: true,
                redeemAddress: RedeemAddress
            )
        );
        await SeedTransactionAsync(
            store,
            CreateTokenTransaction(
                "tx-transfer",
                inputs:
                [CreateInput("tx-issue", 0)],
                outputs:
                [CreateOutput("tx-transfer", 0, HolderB, TokenId, 50, ScriptType.P2STAS)],
                allStasInputsKnown: true,
                illegalRoots: []
            )
        );

        var txJournal = new RavenObservationJournal<TxObservation>(store);
        await txJournal.AppendAsync(CreateObservation(TxObservationEventType.SeenInBlock, "tx-issue", 1, "block-1", 100));
        await txJournal.AppendAsync(CreateObservation(TxObservationEventType.SeenInMempool, "tx-transfer", 2));

        var rebuilder = new TokenProjectionRebuilder(
            store,
            new RavenObservationJournalReader(store),
            new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store))
        );
        var reader = new TokenProjectionReader(store);

        await rebuilder.RebuildAsync();

        using (var session = store.OpenAsyncSession())
        {
            session.Delete(await session.LoadAsync<MetaTransaction>("tx-transfer"));
            session.Delete(await session.LoadAsync<MetaOutput>(MetaOutput.GetId("tx-transfer", 0)));
            await session.SaveChangesAsync();
        }

        await txJournal.AppendAsync(CreateObservation(TxObservationEventType.DroppedBySource, "tx-transfer", 3));
        await rebuilder.RebuildAsync();

        var state = await reader.LoadStateAsync(TokenId);
        var history = await reader.LoadHistoryAsync(TokenId);
        var utxos = await reader.LoadUtxosAsync(TokenId);
        var application = await LoadAsync<TokenProjectionAppliedTransactionDocument>(store, TokenProjectionAppliedTransactionDocument.GetId("tx-transfer"));

        Assert.NotNull(state);
        Assert.Equal(TokenProjectionValidationStatus.Valid, state.ValidationStatus);
        Assert.Equal(50, state.TotalKnownSupply);
        Assert.Single(utxos);
        Assert.Equal("tx-issue", utxos[0].TxId);
        Assert.Single(history);
        Assert.Equal("tx-issue", history[0].TxId);
        Assert.NotNull(application);
        Assert.Equal(TokenProjectionApplicationState.None, application.AppliedState);
        Assert.Empty(application.HistoryDocumentIds);
    }

    private static ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> CreateObservation(
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
            blockHeight);

        var fingerprint = eventType == TxObservationEventType.SeenInBlock
            ? $"node|{eventType}|{txId}|{blockHash}"
            : $"node|{eventType}|{txId}";

        return new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
            new ObservationJournalEntry<TxObservation>(observation),
            new DedupeFingerprint(fingerprint));
    }

    private static MetaTransaction CreateTokenTransaction(
        string txId,
        MetaTransaction.Input[]? inputs = null,
        MetaOutput[]? outputs = null,
        bool isIssue = false,
        bool isValidIssue = false,
        bool allStasInputsKnown = false,
        List<string>? illegalRoots = null,
        string? redeemAddress = null)
        => new()
        {
            Id = txId,
            Inputs = inputs ?? [],
            Outputs = outputs?.Select(x => new MetaTransaction.Output(x)).ToList() ?? [],
            Addresses = outputs?.Select(x => x.Address).Where(x => x != null).Distinct().ToList() ?? [],
            TokenIds = outputs?.Select(x => x.TokenId).Where(x => x != null).Distinct().ToList() ?? [],
            IsStas = true,
            IsIssue = isIssue,
            IsValidIssue = isValidIssue,
            AllStasInputsKnown = allStasInputsKnown,
            IllegalRoots = illegalRoots ?? [],
            MissingTransactions = [],
            RedeemAddress = redeemAddress
        };

    private static MetaTransaction.Input CreateInput(string txId, int vout)
        => new()
        {
            Id = MetaOutput.GetId(txId, vout),
            TxId = txId,
            Vout = vout
        };

    private static MetaOutput CreateOutput(string txId, int vout, string address, string tokenId, long satoshis, ScriptType type)
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
            var metaOutput = new MetaOutput
            {
                Id = output.Id,
                TxId = transaction.Id,
                Vout = output.Id == null ? 0 : int.Parse(output.Id!.Split(':')[1]),
                Address = output.Address,
                TokenId = output.TokenId,
                Satoshis = output.Satoshis,
                Type = output.Type,
                ScriptPubKey = $"script-{transaction.Id}-{output.Id!.Split(':')[1]}",
                Spent = false
            };

            await session.StoreAsync(metaOutput, metaOutput.Id);
        }

        await session.SaveChangesAsync();
    }

    private static async Task<T?> LoadAsync<T>(IDocumentStore store, string id)
    {
        using var session = store.OpenAsyncSession();
        return await session.LoadAsync<T>(id);
    }
}
