using System.Diagnostics;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Impl;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Script;
using Dxs.Consigliere.Benchmarks.Shared;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;

using Microsoft.Extensions.Logging.Abstractions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Benchmarks.AddressHistory;

public sealed class AddressHistoryBenchmarkHarness : ConfiguredRavenBenchmarkTestDriver
{
    public async Task<AddressHistoryBenchmarkMetrics> MeasureRebuildAsync(
        AddressHistoryBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        await SeedScenarioAsync(store, scenario, cancellationToken);

        var rebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var sw = Stopwatch.StartNew();
        await rebuilder.RebuildAsync(take: 8, cancellationToken: cancellationToken);
        sw.Stop();

        var projectedTransactions = scenario.TransferCount * 2;
        return new AddressHistoryBenchmarkMetrics(
            $"{scenario.Name}:rebuild",
            projectedTransactions,
            0,
            0,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(projectedTransactions, sw.ElapsedMilliseconds));
    }

    public async Task<AddressHistoryBenchmarkMetrics> MeasureQueryAsync(
        AddressHistoryBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
        => await MeasureQueryCoreAsync(scenario, stripEnvelope: false, cancellationToken);

    public async Task<AddressHistoryBenchmarkMetrics> MeasureLegacyQueryFallbackAsync(
        AddressHistoryBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
        => await MeasureQueryCoreAsync(scenario, stripEnvelope: true, cancellationToken);

    private async Task<AddressHistoryBenchmarkMetrics> MeasureQueryCoreAsync(
        AddressHistoryBenchmarkScenario scenario,
        bool stripEnvelope,
        CancellationToken cancellationToken)
    {
        using var store = GetDocumentStore();
        await SeedScenarioAsync(store, scenario, cancellationToken);

        var rebuilder = new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store));
        await rebuilder.RebuildAsync(take: 8, cancellationToken: cancellationToken);
        if (stripEnvelope)
            await StripHistoryEnvelopeAsync(store, cancellationToken);

        using var service = new AddressHistoryService(
            store,
            new FilteredTransactionMessageBus(),
            new NoopConnectionManager(),
            new MainnetNetworkProvider(),
            NullLogger<AddressHistoryService>.Instance);
        var request = new GetAddressHistoryRequest(
            VNextBenchmarkFixtureFactory.TargetAddress(),
            ["bsv", VNextBenchmarkFixtureFactory.TokenId(0)],
            Desc: true,
            SkipZeroBalance: false,
            Skip: 0,
            Take: scenario.Take);

        var totalRows = 0;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < scenario.QueryCount; i++)
        {
            var response = await service.GetHistory(request);
            totalRows += response.History.Length;
        }
        sw.Stop();

        return new AddressHistoryBenchmarkMetrics(
            $"{scenario.Name}:{(stripEnvelope ? "legacy-query" : "query")}",
            scenario.TransferCount * 2,
            scenario.QueryCount,
            totalRows,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.QueryCount, sw.ElapsedMilliseconds));
    }

    private static async Task SeedScenarioAsync(IDocumentStore store, AddressHistoryBenchmarkScenario scenario, CancellationToken cancellationToken)
    {
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var tokenId = VNextBenchmarkFixtureFactory.TokenId(0);
        var targetAddress = VNextBenchmarkFixtureFactory.TargetAddress();

        for (var i = 0; i < scenario.TransferCount; i++)
        {
            var issuer = VNextBenchmarkFixtureFactory.IssuerAddress(i);
            var issueTxId = VNextBenchmarkFixtureFactory.IssueTxId(i);
            var transferTxId = VNextBenchmarkFixtureFactory.TransferTxId(i);
            var blockHash = VNextBenchmarkFixtureFactory.BlockHash(i);

            await VNextBenchmarkFixtureFactory.SeedTransactionAsync(
                store,
                VNextBenchmarkFixtureFactory.CreateTransaction(
                    issueTxId,
                    outputs:
                    [
                        VNextBenchmarkFixtureFactory.CreateOutput(issueTxId, 0, issuer, null, 1000, ScriptType.P2PKH),
                        VNextBenchmarkFixtureFactory.CreateOutput(issueTxId, 1, issuer, tokenId, 50, ScriptType.P2STAS)
                    ],
                    isIssue: true,
                    isValidIssue: true,
                    timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_300_000 + i),
                    height: 200 + i,
                    block: blockHash),
                cancellationToken);

            await VNextBenchmarkFixtureFactory.SeedTransactionAsync(
                store,
                VNextBenchmarkFixtureFactory.CreateTransaction(
                    transferTxId,
                    inputs:
                    [
                        VNextBenchmarkFixtureFactory.CreateInput(issueTxId, 0),
                        VNextBenchmarkFixtureFactory.CreateInput(issueTxId, 1)
                    ],
                    outputs:
                    [
                        VNextBenchmarkFixtureFactory.CreateOutput(transferTxId, 0, targetAddress, null, 900, ScriptType.P2PKH),
                        VNextBenchmarkFixtureFactory.CreateOutput(transferTxId, 1, targetAddress, tokenId, 50, ScriptType.P2STAS)
                    ],
                    allStasInputsKnown: true,
                    illegalRoots: [],
                    timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_300_500 + i),
                    height: 200 + i,
                    block: blockHash),
                cancellationToken);

            await txJournal.AppendAsync(
                VNextBenchmarkFixtureFactory.CreateTxObservation(TxObservationEventType.SeenInBlock, issueTxId, i * 2L + 1, blockHash, 200 + i),
                cancellationToken);
            await txJournal.AppendAsync(
                VNextBenchmarkFixtureFactory.CreateTxObservation(TxObservationEventType.SeenInBlock, transferTxId, i * 2L + 2, blockHash, 200 + i),
                cancellationToken);
        }
    }

    private static double ToThroughputPerSecond(int operations, long elapsedMilliseconds)
        => elapsedMilliseconds <= 0
            ? operations
            : operations * 1000.0 / elapsedMilliseconds;

    private static async Task StripHistoryEnvelopeAsync(IDocumentStore store, CancellationToken cancellationToken)
    {
        using var session = store.OpenAsyncSession();
        var applications = await session.Query<AddressProjectionAppliedTransactionDocument>().ToListAsync(token: cancellationToken);
        foreach (var application in applications)
        {
            application.Timestamp = null;
            application.Height = null;
            application.ValidStasTx = null;
            application.TxFeeSatoshis = null;
            application.Note = null;
            application.FromAddresses = [];
            application.ToAddresses = [];
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    private sealed class MainnetNetworkProvider : INetworkProvider
    {
        public Network Network => Network.Mainnet;
    }

    private sealed class NoopConnectionManager : IConnectionManager
    {
        public Task OnAddressBalanceChanged(string transactionId, string address) => Task.CompletedTask;

        public Task SubscribeToDeletedTransactionStream(string connectionId) => Task.CompletedTask;

        public Task SubscribeToTokenStream(string connectionId, string tokenId) => Task.CompletedTask;

        public Task SubscribeToTransactionStream(string connectionId, string address) => Task.CompletedTask;

        public Task UnsubscribeToDeletedTransactionStream(string connectionId) => Task.CompletedTask;

        public Task UnsubscribeToTokenStream(string connectionId, string tokenId) => Task.CompletedTask;

        public Task UnsubscribeToTransactionStream(string connectionId, string address) => Task.CompletedTask;
    }
}
