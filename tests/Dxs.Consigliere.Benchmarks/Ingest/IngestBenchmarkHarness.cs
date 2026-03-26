using System.Diagnostics;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.Benchmarks.Shared;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Benchmarks.Ingest;

public sealed class IngestBenchmarkHarness : ConfiguredRavenBenchmarkTestDriver
{
    public async Task<IngestBenchmarkMetrics> MeasureTxIngestAsync(
        IngestBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        var writer = CreateTxWriter(store, scenario.PersistPayloads);

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < scenario.TxObservationCount; i++)
        {
            var txId = VNextBenchmarkFixtureFactory.TransferTxId(i);
            var transaction = VNextBenchmarkFixtureFactory.CreateRuntimeTransaction(txId, [VNextBenchmarkFixtureFactory.TokenId(i)]);
            await writer.AppendAsync(
                TxMessage.AddedToMempool(transaction, 1_710_200_000 + i, TxObservationSource.Node),
                cancellationToken);
        }
        sw.Stop();

        return new IngestBenchmarkMetrics(
            $"{scenario.Name}:tx",
            scenario.TxObservationCount,
            0,
            scenario.PersistPayloads ? scenario.TxObservationCount : 0,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.TxObservationCount, sw.ElapsedMilliseconds));
    }

    public async Task<IngestBenchmarkMetrics> MeasureBlockIngestAsync(
        IngestBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        var writer = new BlockObservationJournalWriter(new RavenObservationJournal<BlockObservation>(store));

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < scenario.BlockObservationCount; i++)
        {
            await writer.AppendConnectedAsync(
                new BlockMessage(VNextBenchmarkFixtureFactory.BlockHash(i), TxObservationSource.Node),
                cancellationToken);
        }
        sw.Stop();

        return new IngestBenchmarkMetrics(
            $"{scenario.Name}:block",
            0,
            scenario.BlockObservationCount,
            0,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.BlockObservationCount, sw.ElapsedMilliseconds));
    }

    public async Task<IngestBenchmarkMetrics> MeasureMixedBurstAsync(
        IngestBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        var txWriter = CreateTxWriter(store, scenario.PersistPayloads);
        var blockWriter = new BlockObservationJournalWriter(new RavenObservationJournal<BlockObservation>(store));

        var operations = Math.Max(scenario.TxObservationCount, scenario.BlockObservationCount);
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < operations; i++)
        {
            if (i < scenario.TxObservationCount)
            {
                var txId = VNextBenchmarkFixtureFactory.TransferTxId(i);
                var transaction = VNextBenchmarkFixtureFactory.CreateRuntimeTransaction(txId, [VNextBenchmarkFixtureFactory.TokenId(i)]);
                await txWriter.AppendAsync(
                    TxMessage.FoundInBlock(transaction, 1_710_200_500 + i, TxObservationSource.JungleBus, VNextBenchmarkFixtureFactory.BlockHash(i), 300 + i, i),
                    cancellationToken);
            }

            if (i < scenario.BlockObservationCount)
            {
                await blockWriter.AppendConnectedAsync(
                    new BlockMessage(VNextBenchmarkFixtureFactory.BlockHash(i), TxObservationSource.Node),
                    cancellationToken);
            }
        }
        sw.Stop();

        var totalOperations = scenario.TxObservationCount + scenario.BlockObservationCount;
        return new IngestBenchmarkMetrics(
            $"{scenario.Name}:mixed",
            scenario.TxObservationCount,
            scenario.BlockObservationCount,
            scenario.PersistPayloads ? scenario.TxObservationCount : 0,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(totalOperations, sw.ElapsedMilliseconds));
    }

    private static TxObservationJournalWriter CreateTxWriter(Raven.Client.Documents.IDocumentStore store, bool persistPayloads)
        => new(
            new RavenObservationJournal<TxObservation>(store),
            new RavenRawTransactionPayloadStore(store),
            Options.Create(new ConsigliereStorageConfig
            {
                RawTransactionPayloads = new RawTransactionPayloadsStorageConfig
                {
                    Enabled = persistPayloads,
                    Provider = "raven"
                }
            }),
            NullLogger<TxObservationJournalWriter>.Instance);

    private static double ToThroughputPerSecond(int operations, long elapsedMilliseconds)
        => elapsedMilliseconds <= 0
            ? operations
            : operations * 1000.0 / elapsedMilliseconds;
}
