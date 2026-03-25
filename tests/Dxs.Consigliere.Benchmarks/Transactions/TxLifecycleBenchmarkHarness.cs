using System.Diagnostics;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Services.Impl;

using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Benchmarks.Transactions;

public sealed class TxLifecycleBenchmarkHarness : RavenTestDriver
{
    static TxLifecycleBenchmarkHarness()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }

    public async Task<TxLifecycleBenchmarkMetrics> MeasureRebuildAsync(
        TxLifecycleBenchmarkScenario scenario,
        CancellationToken cancellationToken = default
    )
    {
        using var store = GetDocumentStore();
        await SeedScenarioAsync(store, scenario, cancellationToken);

        var rebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var sw = Stopwatch.StartNew();
        await rebuilder.RebuildAsync(cancellationToken: cancellationToken);
        sw.Stop();

        var observations = scenario.TransactionCount * 2;
        return new TxLifecycleBenchmarkMetrics(
            $"{scenario.Name}:rebuild",
            observations,
            0,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(observations, sw.ElapsedMilliseconds)
        );
    }

    public async Task<TxLifecycleBenchmarkMetrics> MeasureQueryAsync(
        TxLifecycleBenchmarkScenario scenario,
        CancellationToken cancellationToken = default
    )
    {
        using var store = GetDocumentStore();
        await SeedScenarioAsync(store, scenario, cancellationToken);

        var reader = new TxLifecycleProjectionReader(store);
        var rebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var service = new TransactionQueryService(store, reader, rebuilder);

        await rebuilder.RebuildAsync(cancellationToken: cancellationToken);

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < scenario.TransactionCount; i++)
        {
            await service.GetTransactionStateAsync(TxId(i), cancellationToken);
        }

        sw.Stop();

        return new TxLifecycleBenchmarkMetrics(
            $"{scenario.Name}:query",
            scenario.TransactionCount * 2,
            scenario.TransactionCount,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.TransactionCount, sw.ElapsedMilliseconds)
        );
    }

    private static async Task SeedScenarioAsync(
        Raven.Client.Documents.IDocumentStore store,
        TxLifecycleBenchmarkScenario scenario,
        CancellationToken cancellationToken
    )
    {
        var txJournal = new RavenObservationJournal<TxObservation>(store);

        for (var i = 0; i < scenario.TransactionCount; i++)
        {
            await txJournal.AppendAsync(
                new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
                    new ObservationJournalEntry<TxObservation>(
                        new TxObservation(
                            TxObservationEventType.SeenInMempool,
                            TxObservationSource.Node,
                            TxId(i),
                            DateTimeOffset.FromUnixTimeSeconds(1_710_000_000 + i)
                        )
                    ),
                    new DedupeFingerprint($"node|tx_seen_in_mempool|{TxId(i)}")
                ),
                cancellationToken
            );

            await txJournal.AppendAsync(
                new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
                    new ObservationJournalEntry<TxObservation>(
                        new TxObservation(
                            TxObservationEventType.SeenInBlock,
                            TxObservationSource.Node,
                            TxId(i),
                            DateTimeOffset.FromUnixTimeSeconds(1_710_000_500 + i),
                            $"block-{i / 4}",
                            i / 4,
                            i
                        )
                    ),
                    new DedupeFingerprint($"node|tx_seen_in_block|{TxId(i)}|block-{i / 4}")
                ),
                cancellationToken
            );
        }
    }

    private static double ToThroughputPerSecond(int operations, long elapsedMilliseconds)
        => elapsedMilliseconds <= 0
            ? operations
            : operations * 1000.0 / elapsedMilliseconds;

    private static string TxId(int index) => index.ToString("x64");
}
