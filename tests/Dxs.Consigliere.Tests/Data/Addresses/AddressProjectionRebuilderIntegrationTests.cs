using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Script;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Tests.Shared;

using Raven.Client.Documents;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Addresses;

public class AddressProjectionRebuilderIntegrationTests : RavenTestDriver
{
    private const string TokenId = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string Address1 = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    private const string Address2 = "1BoatSLRHtKNngkdXEeobR76b53LETtpyT";
    private const string Address3 = "1KFHE7w8BhaENAswwryaoccDb6qcT6DbYY";

    [Fact]
    public async Task RebuildAsync_ProjectsBalancesAndUtxosAcrossBsvAndTokenTransfers()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTransactionAsync(
            store,
            CreateTransaction(
                "tx-1",
                outputs:
                [
                    CreateOutput("tx-1", 0, Address1, null, 1000, ScriptType.P2PKH),
                    CreateOutput("tx-1", 1, Address1, TokenId, 50, ScriptType.P2STAS)
                ],
                isIssue: true,
                isValidIssue: true
            )
        );
        await SeedTransactionAsync(
            store,
            CreateTransaction(
                "tx-2",
                inputs:
                [
                    CreateInput("tx-1", 0),
                    CreateInput("tx-1", 1)
                ],
                outputs:
                [
                    CreateOutput("tx-2", 0, Address2, null, 900, ScriptType.P2PKH),
                    CreateOutput("tx-2", 1, Address3, TokenId, 50, ScriptType.P2STAS)
                ],
                allStasInputsKnown: true,
                illegalRoots: []
            )
        );

        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var rebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var reader = new AddressProjectionReader(store);

        await txJournal.AppendAsync(CreateTxObservation(TxObservationEventType.SeenInBlock, "tx-1", 1, "block-1", 100));
        await txJournal.AppendAsync(CreateTxObservation(TxObservationEventType.SeenInMempool, "tx-2", 2));

        var checkpoint = await rebuilder.RebuildAsync();
        var bsvBalances = await reader.LoadBsvBalancesAsync([Address1, Address2, Address3]);
        var tokenBalances = await reader.LoadTokenBalancesAsync([Address1, Address2, Address3], [TokenId]);
        var address2Utxos = await reader.LoadUtxosAsync(Address2, null);
        var address3TokenUtxos = await reader.LoadUtxosAsync(Address3, TokenId);

        Assert.Equal(2, checkpoint.Sequence.Value);
        Assert.Single(bsvBalances);
        Assert.Equal(Address2, bsvBalances[0].Address);
        Assert.Equal(900, bsvBalances[0].Satoshis);
        Assert.Single(tokenBalances);
        Assert.Equal(Address3, tokenBalances[0].Address);
        Assert.Equal(50, tokenBalances[0].Satoshis);
        Assert.Single(address2Utxos);
        Assert.Equal("tx-2", address2Utxos[0].TxId);
        Assert.Single(address3TokenUtxos);
        Assert.Equal("tx-2", address3TokenUtxos[0].TxId);
    }

    [Fact]
    public async Task RebuildAsync_RevertsFromStoredMutationFactsAfterLegacyDeletionAndDropObservation()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTransactionAsync(
            store,
            CreateTransaction(
                "tx-1",
                outputs:
                [
                    CreateOutput("tx-1", 0, Address1, null, 1000, ScriptType.P2PKH),
                    CreateOutput("tx-1", 1, Address1, TokenId, 50, ScriptType.P2STAS)
                ],
                isIssue: true,
                isValidIssue: true
            )
        );
        await SeedTransactionAsync(
            store,
            CreateTransaction(
                "tx-2",
                inputs:
                [
                    CreateInput("tx-1", 0),
                    CreateInput("tx-1", 1)
                ],
                outputs:
                [
                    CreateOutput("tx-2", 0, Address2, null, 900, ScriptType.P2PKH),
                    CreateOutput("tx-2", 1, Address3, TokenId, 50, ScriptType.P2STAS)
                ],
                allStasInputsKnown: true,
                illegalRoots: []
            )
        );

        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var rebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var reader = new AddressProjectionReader(store);

        await txJournal.AppendAsync(CreateTxObservation(TxObservationEventType.SeenInBlock, "tx-1", 1, "block-1", 100));
        await txJournal.AppendAsync(CreateTxObservation(TxObservationEventType.SeenInMempool, "tx-2", 2));
        await rebuilder.RebuildAsync();

        using (var session = store.OpenAsyncSession())
        {
            var tx2 = await session.LoadAsync<MetaTransaction>("tx-2");
            var out0 = await session.LoadAsync<MetaOutput>(MetaOutput.GetId("tx-2", 0));
            var out1 = await session.LoadAsync<MetaOutput>(MetaOutput.GetId("tx-2", 1));
            session.Delete(tx2);
            session.Delete(out0);
            session.Delete(out1);
            await session.SaveChangesAsync();
        }

        await txJournal.AppendAsync(CreateTxObservation(TxObservationEventType.DroppedBySource, "tx-2", 3));
        await rebuilder.RebuildAsync();

        var bsvBalances = await reader.LoadBsvBalancesAsync([Address1, Address2]);
        var tokenBalances = await reader.LoadTokenBalancesAsync([Address1, Address3], [TokenId]);
        var address1BsvUtxos = await reader.LoadUtxosAsync(Address1, null);
        var address1TokenUtxos = await reader.LoadUtxosAsync(Address1, TokenId);
        var application = await LoadAsync<AddressProjectionAppliedTransactionDocument>(store, AddressProjectionAppliedTransactionDocument.GetId("tx-2"));

        Assert.Single(bsvBalances);
        Assert.Equal(Address1, bsvBalances[0].Address);
        Assert.Equal(1000, bsvBalances[0].Satoshis);
        Assert.Single(tokenBalances);
        Assert.Equal(Address1, tokenBalances[0].Address);
        Assert.Equal(50, tokenBalances[0].Satoshis);
        Assert.Single(address1BsvUtxos);
        Assert.Single(address1TokenUtxos);
        Assert.NotNull(application);
        Assert.Equal(AddressProjectionApplicationState.None, application.AppliedState);
        Assert.Empty(application.Credits);
        Assert.Empty(application.Debits);
    }

    private static ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> CreateTxObservation(
        string eventType,
        string txId,
        long second,
        string? blockHash = null,
        int? blockHeight = null
    )
    {
        var observation = new TxObservation(
            eventType,
            TxObservationSource.Node,
            txId,
            DateTimeOffset.FromUnixTimeSeconds(1_710_000_000 + second),
            blockHash,
            blockHeight
        );

        var fingerprint = eventType switch
        {
            var x when x == TxObservationEventType.SeenInBlock => $"node|{eventType}|{txId}|{blockHash}",
            _ => $"node|{eventType}|{txId}"
        };

        return new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
            new ObservationJournalEntry<TxObservation>(observation),
            new DedupeFingerprint(fingerprint)
        );
    }

    private static MetaTransaction CreateTransaction(
        string txId,
        MetaTransaction.Input[]? inputs = null,
        MetaOutput[]? outputs = null,
        bool isIssue = false,
        bool isValidIssue = false,
        bool allStasInputsKnown = false,
        List<string>? illegalRoots = null
    )
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
            MissingTransactions = []
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
