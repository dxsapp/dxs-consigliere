using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Script;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using Raven.Client.Documents;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class UtxoSetManagerProjectionTests : RavenTestDriver
{
    private const string Address1 = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    private const string Address2 = "1BoatSLRHtKNngkdXEeobR76b53LETtpyT";

    [Fact]
    public async Task GetBalanceAndUtxoSet_ReadFromAddressProjection()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTransactionAsync(store, "tx-1", Address1, 1000);
        await SeedTransactionAsync(store, "tx-2", Address2, 900, "tx-1", 0);

        var txJournal = new RavenObservationJournal<TxObservation>(store);
        await txJournal.AppendAsync(CreateObservation(TxObservationEventType.SeenInBlock, "tx-1", 1, "block-1", 100));
        await txJournal.AppendAsync(CreateObservation(TxObservationEventType.SeenInMempool, "tx-2", 2));

        var manager = new UtxoSetManager(
            store,
            new TestNetworkProvider(),
            new AddressProjectionReader(store),
            new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store))
        );

        var balances = await manager.GetBalance(new BalanceRequest([Address1, Address2], null));
        var utxos = await manager.GetUtxoSet(new GetUtxoSetRequest(null, Address2, null));

        Assert.Single(balances);
        Assert.Equal(Address2, balances[0].Address);
        Assert.Equal(900, balances[0].Satoshis);
        Assert.Single(utxos.UtxoSet);
        Assert.Equal("tx-2", utxos.UtxoSet[0].TxId);
        Assert.Equal(900, utxos.UtxoSet[0].Satoshis);
    }

    private static ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> CreateObservation(
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

    private static async Task SeedTransactionAsync(IDocumentStore store, string txId, string address, long satoshis, string? spentTxId = null, int spentVout = 0)
    {
        using var session = store.OpenAsyncSession();

        if (spentTxId != null)
        {
            var previousOutput = await session.LoadAsync<MetaOutput>(MetaOutput.GetId(spentTxId, spentVout));
            previousOutput.Spent = true;
            previousOutput.SpendTxId = txId;
        }

        var output = new MetaOutput
        {
            Id = MetaOutput.GetId(txId, 0),
            TxId = txId,
            Vout = 0,
            Address = address,
            Satoshis = satoshis,
            Type = ScriptType.P2PKH,
            ScriptPubKey = $"script-{txId}-0",
            Spent = false
        };

        var transaction = new MetaTransaction
        {
            Id = txId,
            Inputs = spentTxId == null
                ? []
                : [new MetaTransaction.Input { Id = MetaOutput.GetId(spentTxId, spentVout), TxId = spentTxId, Vout = spentVout }],
            Outputs = [new MetaTransaction.Output(output)],
            Addresses = [address],
            TokenIds = [],
            IllegalRoots = [],
            MissingTransactions = []
        };

        await session.StoreAsync(output, output.Id);
        await session.StoreAsync(transaction, transaction.Id);
        await session.SaveChangesAsync();
    }

    private sealed class TestNetworkProvider : INetworkProvider
    {
        public Network Network => Network.Mainnet;
    }
}
