using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Script;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class AddressHistoryServiceProjectionTests : RavenTestDriver
{
    static AddressHistoryServiceProjectionTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }

    private const string IssuerAddress = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    private const string ReceiverAddress = "1KFHE7w8BhaENAswwryaoccDb6qcT6DbYY";
    private const string TokenId = "1111111111111111111111111111111111111111";
    private const string IssueTxId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string TransferTxId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task GetHistory_ReturnsProjectionBackedBsvHistory()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedScenarioAsync(store);

        var service = CreateService(store);
        var response = await service.GetHistory(new GetAddressHistoryRequest(IssuerAddress, null, false, false, 0, 100));

        Assert.Equal(2, response.TotalCount);
        Assert.Equal(2, response.History.Length);
        Assert.Equal(IssueTxId, response.History[0].TxId);
        Assert.Equal(TransferTxId, response.History[1].TxId);
        Assert.Null(response.History[0].TokenId);
        Assert.Null(response.History[1].TokenId);
        Assert.Equal(1000, response.History[0].ReceivedSatoshis);
        Assert.Equal(1000, response.History[1].SpentSatoshis);
        Assert.Equal(1000, response.History[0].BalanceSatoshis);
        Assert.Equal(-1000, response.History[1].BalanceSatoshis);
        Assert.Equal(1_710_000_000, response.History[0].Timestamp);
        Assert.Equal(1_710_000_100, response.History[1].Timestamp);
    }

    [Fact]
    public async Task GetHistory_ReturnsProjectionBackedTokenHistory()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedScenarioAsync(store);

        var service = CreateService(store);
        var response = await service.GetHistory(new GetAddressHistoryRequest(IssuerAddress, [TokenId], false, false, 0, 100));

        Assert.Equal(2, response.TotalCount);
        Assert.Equal(2, response.History.Length);
        Assert.Equal(IssueTxId, response.History[0].TxId);
        Assert.Equal(TransferTxId, response.History[1].TxId);
        Assert.Equal(TokenId, response.History[0].TokenId);
        Assert.Equal(TokenId, response.History[1].TokenId);
        Assert.Equal(50, response.History[0].ReceivedSatoshis);
        Assert.Equal(50, response.History[1].SpentSatoshis);
        Assert.Equal(50, response.History[0].BalanceSatoshis);
        Assert.Equal(-50, response.History[1].BalanceSatoshis);
    }

    private static AddressHistoryService CreateService(IDocumentStore store)
    {
        var bus = new Mock<IFilteredTransactionMessageBus>(MockBehavior.Strict);
        bus.Setup(x => x.Subscribe(It.IsAny<IObserver<FilteredTransactionMessage>>()))
            .Returns(new NoopDisposable());

        return new AddressHistoryService(
            store,
            bus.Object,
            Mock.Of<IConnectionManager>(),
            Mock.Of<INetworkProvider>(x => x.Network == Dxs.Bsv.Network.Mainnet),
            NullLogger<AddressHistoryService>.Instance
        );
    }

    private static async Task SeedScenarioAsync(IDocumentStore store)
    {
        using var session = store.OpenAsyncSession();

        var issueTransaction = CreateTransaction(
            IssueTxId,
            1_710_000_000,
            1,
            outputs:
            [
                CreateOutput(IssueTxId, 0, IssuerAddress, null, 1000, ScriptType.P2PKH),
                CreateOutput(IssueTxId, 1, IssuerAddress, TokenId, 50, ScriptType.P2STAS)
            ],
            isIssue: true,
            isValidIssue: true,
            redeemAddress: IssuerAddress);

        var transferTransaction = CreateTransaction(
            TransferTxId,
            1_710_000_100,
            2,
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
            illegalRoots: []);

        await session.StoreAsync(issueTransaction, issueTransaction.Id);
        await session.StoreAsync(transferTransaction, transferTransaction.Id);

        await StoreOutputsAsync(session, issueTransaction);
        await StoreOutputsAsync(session, transferTransaction);

        await session.StoreAsync(
            new AddressProjectionAppliedTransactionDocument
            {
                Id = AddressProjectionAppliedTransactionDocument.GetId(IssueTxId),
                TxId = IssueTxId,
                AppliedState = AddressProjectionApplicationState.Confirmed,
                ConfirmedBlockHash = "block-1",
                Credits =
                [
                    CreateSnapshot(IssueTxId, 0, IssuerAddress, null, 1000, ScriptType.P2PKH, "script-issue-0"),
                    CreateSnapshot(IssueTxId, 1, IssuerAddress, TokenId, 50, ScriptType.P2STAS, "script-issue-1")
                ],
                Debits = []
            },
            AddressProjectionAppliedTransactionDocument.GetId(IssueTxId));

        await session.StoreAsync(
            new AddressProjectionAppliedTransactionDocument
            {
                Id = AddressProjectionAppliedTransactionDocument.GetId(TransferTxId),
                TxId = TransferTxId,
                AppliedState = AddressProjectionApplicationState.Pending,
                ConfirmedBlockHash = null,
                Credits =
                [
                    CreateSnapshot(TransferTxId, 0, ReceiverAddress, null, 900, ScriptType.P2PKH, "script-transfer-0"),
                    CreateSnapshot(TransferTxId, 1, ReceiverAddress, TokenId, 50, ScriptType.P2STAS, "script-transfer-1")
                ],
                Debits =
                [
                    CreateSnapshot(IssueTxId, 0, IssuerAddress, null, 1000, ScriptType.P2PKH, "script-issue-0"),
                    CreateSnapshot(IssueTxId, 1, IssuerAddress, TokenId, 50, ScriptType.P2STAS, "script-issue-1")
                ]
            },
            AddressProjectionAppliedTransactionDocument.GetId(TransferTxId));

        await session.SaveChangesAsync();
    }

    private static async Task StoreOutputsAsync(IAsyncDocumentSession session, MetaTransaction transaction)
    {
        foreach (var output in transaction.Outputs)
        {
            var vout = int.Parse(output.Id.Split(':')[1]);
            await session.StoreAsync(new MetaOutput
            {
                Id = output.Id,
                TxId = transaction.Id,
                Vout = vout,
                Address = output.Address,
                TokenId = output.TokenId,
                Satoshis = output.Satoshis,
                Type = output.Type,
                ScriptPubKey = $"script-{transaction.Id}-{vout}",
                Spent = false
            }, output.Id);
        }
    }

    private static AddressProjectionUtxoSnapshot CreateSnapshot(
        string txId,
        int vout,
        string address,
        string? tokenId,
        long satoshis,
        ScriptType scriptType,
        string scriptPubKey
    )
        => new()
        {
            Id = AddressUtxoProjectionDocument.GetId(txId, vout),
            TxId = txId,
            Vout = vout,
            Address = address,
            TokenId = tokenId,
            Satoshis = satoshis,
            ScriptType = scriptType,
            ScriptPubKey = scriptPubKey
        };

    private static MetaTransaction CreateTransaction(
        string txId,
        long timestamp,
        int height,
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
            IsStas = outputs?.Any(x => x.Type is ScriptType.P2STAS or ScriptType.DSTAS) == true,
            IsIssue = isIssue,
            IsValidIssue = isValidIssue,
            AllStasInputsKnown = allStasInputsKnown,
            IllegalRoots = illegalRoots ?? [],
            MissingTransactions = [],
            RedeemAddress = redeemAddress,
            Timestamp = timestamp,
            Height = height,
            Note = $"{txId}-note"
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

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
