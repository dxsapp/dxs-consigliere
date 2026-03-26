using Dxs.Bsv.Script;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Addresses;

public class AddressHistoryEnvelopeBackfillServiceTests : RavenTestDriver
{
    private const string TokenId = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string Address1 = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    private const string Address2 = "1BoatSLRHtKNngkdXEeobR76b53LETtpyT";

    [Fact]
    public async Task BackfillBatchAsync_HydratesLegacyAppliedTransactions()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        using (var session = store.OpenAsyncSession())
        {
            await session.StoreAsync(
                new MetaTransaction
                {
                    Id = "tx-1",
                    Timestamp = 1_710_000_000,
                    Height = 200,
                    Note = "legacy",
                    IsIssue = false,
                    IllegalRoots = [],
                    Inputs = [],
                    Outputs = []
                },
                "tx-1");
            await session.StoreAsync(
                new AddressProjectionAppliedTransactionDocument
                {
                    Id = AddressProjectionAppliedTransactionDocument.GetId("tx-1"),
                    TxId = "tx-1",
                    AppliedState = AddressProjectionApplicationState.Confirmed,
                    Credits =
                    [
                        new AddressProjectionUtxoSnapshot
                        {
                            Id = "tx-1/0",
                            TxId = "tx-1",
                            Vout = 0,
                            Address = Address1,
                            TokenId = TokenId,
                            Satoshis = 25,
                            ScriptType = ScriptType.P2STAS,
                            ScriptPubKey = "51"
                        }
                    ],
                    Debits =
                    [
                        new AddressProjectionUtxoSnapshot
                        {
                            Id = "prev/0",
                            TxId = "prev",
                            Vout = 0,
                            Address = Address2,
                            TokenId = TokenId,
                            Satoshis = 30,
                            ScriptType = ScriptType.P2STAS,
                            ScriptPubKey = "51"
                        }
                    ],
                    LastSequence = 77
                },
                AddressProjectionAppliedTransactionDocument.GetId("tx-1"));
            await session.SaveChangesAsync();
        }

        var service = new AddressHistoryEnvelopeBackfillService(store);
        var result = await service.BackfillBatchAsync(32);
        var telemetry = ((IAddressHistoryEnvelopeBackfillTelemetry)service).GetSnapshot();

        AddressProjectionAppliedTransactionDocument rewritten;
        using (var session = store.OpenAsyncSession())
        {
            rewritten = await session.LoadAsync<AddressProjectionAppliedTransactionDocument>(
                AddressProjectionAppliedTransactionDocument.GetId("tx-1"));
        }

        Assert.Equal(1, result.Scanned);
        Assert.Equal(1, result.Rewritten);
        Assert.Equal(0, result.PendingCount);
        Assert.NotNull(rewritten);
        Assert.Equal(1_710_000_000, rewritten.Timestamp);
        Assert.Equal(200, rewritten.Height);
        Assert.True(rewritten.ValidStasTx);
        Assert.Equal(-5, rewritten.TxFeeSatoshis);
        Assert.Equal(["legacy"], [rewritten.Note]);
        Assert.Equal([Address2], rewritten.FromAddresses);
        Assert.Equal([Address1], rewritten.ToAddresses);
        Assert.Equal(1, telemetry.LastBatchRewritten);
        Assert.Equal(0, telemetry.PendingCount);
        Assert.Equal(77, telemetry.LastTouchedSequence);
    }
}
