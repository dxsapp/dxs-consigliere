using System.Diagnostics;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Script;
using Dxs.Consigliere.Benchmarks.Shared;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Transactions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Benchmarks.Recovery;

public sealed class RecoveryBenchmarkHarness : ConfiguredRavenBenchmarkTestDriver
{
    public async Task<RecoveryBenchmarkMetrics> MeasureReorgRecoveryAsync(
        RecoveryBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        var unstableBlockHash = VNextBenchmarkFixtureFactory.UnstableBlockHash();
        await SeedConfirmedScenarioAsync(store, scenario.TransferCount, unstableBlockHash, cancellationToken);

        var journalReader = new RavenObservationJournalReader(store);
        var txRebuilder = new TxLifecycleProjectionRebuilder(store, journalReader);
        var addressRebuilder = new AddressProjectionRebuilder(store, journalReader);

        await txRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        await addressRebuilder.RebuildAsync(take: 4, cancellationToken: cancellationToken);

        var blockJournal = new RavenObservationJournal<BlockObservation>(store);
        await blockJournal.AppendAsync(
            VNextBenchmarkFixtureFactory.CreateBlockObservation(BlockObservationEventType.Disconnected, unstableBlockHash),
            cancellationToken);

        var sw = Stopwatch.StartNew();
        await txRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        await addressRebuilder.RebuildAsync(take: 4, cancellationToken: cancellationToken);
        sw.Stop();

        return new RecoveryBenchmarkMetrics(
            $"{scenario.Name}:reorg",
            scenario.TransferCount,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.TransferCount, sw.ElapsedMilliseconds));
    }

    public async Task<RecoveryBenchmarkMetrics> MeasureDropRecoveryAsync(
        RecoveryBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        await SeedPendingScenarioAsync(store, scenario.PendingCount, cancellationToken);

        var journalReader = new RavenObservationJournalReader(store);
        var txRebuilder = new TxLifecycleProjectionRebuilder(store, journalReader);
        var addressRebuilder = new AddressProjectionRebuilder(store, journalReader);

        await txRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        await addressRebuilder.RebuildAsync(take: 4, cancellationToken: cancellationToken);

        var txJournal = new RavenObservationJournal<TxObservation>(store);
        for (var i = 0; i < scenario.PendingCount; i++)
        {
            await txJournal.AppendAsync(
                VNextBenchmarkFixtureFactory.CreateTxObservation(
                    TxObservationEventType.DroppedBySource,
                    VNextBenchmarkFixtureFactory.TransferTxId(i),
                    1000 + i,
                    removeReason: nameof(RemoveFromMempoolReason.Reorg)),
                cancellationToken);
        }

        var sw = Stopwatch.StartNew();
        await txRebuilder.RebuildAsync(cancellationToken: cancellationToken);
        await addressRebuilder.RebuildAsync(take: 4, cancellationToken: cancellationToken);
        sw.Stop();

        return new RecoveryBenchmarkMetrics(
            $"{scenario.Name}:drop",
            scenario.PendingCount,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.PendingCount, sw.ElapsedMilliseconds));
    }

    private static async Task SeedConfirmedScenarioAsync(IDocumentStore store, int transferCount, string unstableBlockHash, CancellationToken cancellationToken)
    {
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var tokenId = VNextBenchmarkFixtureFactory.TokenId(0);

        for (var i = 0; i < transferCount; i++)
        {
            var issuer = VNextBenchmarkFixtureFactory.IssuerAddress(i);
            var receiver = VNextBenchmarkFixtureFactory.TargetAddress(i + 1);
            var issueTxId = VNextBenchmarkFixtureFactory.IssueTxId(i);
            var transferTxId = VNextBenchmarkFixtureFactory.TransferTxId(i);
            var stableBlockHash = VNextBenchmarkFixtureFactory.BlockHash(i);

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
                    allStasInputsKnown: true,
                    timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_400_000 + i),
                    height: 300 + i,
                    block: stableBlockHash),
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
                        VNextBenchmarkFixtureFactory.CreateOutput(transferTxId, 0, receiver, null, 900, ScriptType.P2PKH),
                        VNextBenchmarkFixtureFactory.CreateOutput(transferTxId, 1, receiver, tokenId, 50, ScriptType.P2STAS)
                    ],
                    allStasInputsKnown: true,
                    illegalRoots: [],
                    timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_400_500 + i),
                    height: 400,
                    block: unstableBlockHash),
                cancellationToken);

            await txJournal.AppendAsync(
                VNextBenchmarkFixtureFactory.CreateTxObservation(TxObservationEventType.SeenInBlock, issueTxId, i * 2L + 1, stableBlockHash, 300 + i),
                cancellationToken);
            await txJournal.AppendAsync(
                VNextBenchmarkFixtureFactory.CreateTxObservation(TxObservationEventType.SeenInBlock, transferTxId, i * 2L + 2, unstableBlockHash, 400),
                cancellationToken);
        }
    }

    private static async Task SeedPendingScenarioAsync(IDocumentStore store, int pendingCount, CancellationToken cancellationToken)
    {
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var tokenId = VNextBenchmarkFixtureFactory.TokenId(0);

        for (var i = 0; i < pendingCount; i++)
        {
            var issuer = VNextBenchmarkFixtureFactory.IssuerAddress(i);
            var receiver = VNextBenchmarkFixtureFactory.TargetAddress(i + 1);
            var issueTxId = VNextBenchmarkFixtureFactory.IssueTxId(i);
            var transferTxId = VNextBenchmarkFixtureFactory.TransferTxId(i);
            var stableBlockHash = VNextBenchmarkFixtureFactory.BlockHash(i);

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
                    allStasInputsKnown: true,
                    timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_401_000 + i),
                    height: 500 + i,
                    block: stableBlockHash),
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
                        VNextBenchmarkFixtureFactory.CreateOutput(transferTxId, 0, receiver, null, 900, ScriptType.P2PKH),
                        VNextBenchmarkFixtureFactory.CreateOutput(transferTxId, 1, receiver, tokenId, 50, ScriptType.P2STAS)
                    ],
                    allStasInputsKnown: true,
                    illegalRoots: [],
                    timestamp: DateTimeOffset.FromUnixTimeSeconds(1_710_401_500 + i)),
                cancellationToken);

            await txJournal.AppendAsync(
                VNextBenchmarkFixtureFactory.CreateTxObservation(TxObservationEventType.SeenInBlock, issueTxId, i * 2L + 1, stableBlockHash, 500 + i),
                cancellationToken);
            await txJournal.AppendAsync(
                VNextBenchmarkFixtureFactory.CreateTxObservation(TxObservationEventType.SeenInMempool, transferTxId, i * 2L + 2),
                cancellationToken);
        }
    }

    private static double ToThroughputPerSecond(int operations, long elapsedMilliseconds)
        => elapsedMilliseconds <= 0
            ? operations
            : operations * 1000.0 / elapsedMilliseconds;
}
