using System.Diagnostics;

using Dxs.Common.Journal;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;

using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Benchmarks.Journal;

public sealed class JournalBenchmarkHarness : RavenTestDriver
{
    static JournalBenchmarkHarness()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }

    public async Task<JournalBenchmarkMetrics> MeasureAppendAsync(
        JournalBenchmarkScenario scenario,
        CancellationToken cancellationToken = default
    )
    {
        using var store = GetDocumentStore();
        var payloadStore = new RavenRawTransactionPayloadStore(store);
        var journal = new RavenObservationJournal<JournalObservation>(store);

        var sw = Stopwatch.StartNew();
        var lastSequence = JournalSequence.Empty;

        for (var i = 0; i < scenario.ObservationCount; i++)
        {
            var payloadReference = scenario.IncludePayloadReferences && i % 5 == 0
                ? await payloadStore.SaveAsync(
                    TxId(i),
                    PayloadHex(i),
                    RawTransactionPayloadCompressionAlgorithm.None,
                    cancellationToken
                )
                : null;

            var result = await journal.AppendAsync(
                new ObservationJournalAppendRequest<ObservationJournalEntry<JournalObservation>>(
                    new ObservationJournalEntry<JournalObservation>(
                        new JournalObservation(
                            "tx_seen_by_source",
                            TxId(i),
                            i % 3 == 0 ? $"block-{i / 3}" : null,
                            i % 3 == 0 ? i / 3 : null,
                            "node"
                        ),
                        payloadReference
                    ),
                    new DedupeFingerprint($"node|tx_seen_by_source|{TxId(i)}")
                ),
                cancellationToken
            );

            lastSequence = result.Sequence;
        }

        sw.Stop();

        return new JournalBenchmarkMetrics(
            $"{scenario.Name}:append",
            scenario.ObservationCount,
            0,
            0,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.ObservationCount, sw.ElapsedMilliseconds),
            lastSequence.Value
        );
    }

    public async Task<JournalBenchmarkMetrics> MeasureReplayAsync(
        JournalBenchmarkScenario scenario,
        CancellationToken cancellationToken = default
    )
    {
        using var store = GetDocumentStore();
        var payloadStore = new RavenRawTransactionPayloadStore(store);
        var journal = new RavenObservationJournal<JournalObservation>(store);

        for (var i = 0; i < scenario.ObservationCount; i++)
        {
            var payloadReference = scenario.IncludePayloadReferences && i % 5 == 0
                ? await payloadStore.SaveAsync(
                    TxId(i),
                    PayloadHex(i),
                    RawTransactionPayloadCompressionAlgorithm.None,
                    cancellationToken
                )
                : null;

            await journal.AppendAsync(
                new ObservationJournalAppendRequest<ObservationJournalEntry<JournalObservation>>(
                    new ObservationJournalEntry<JournalObservation>(
                        new JournalObservation("tx_seen_in_block", TxId(i), $"block-{i / 3}", i / 3, "node"),
                        payloadReference
                    ),
                    new DedupeFingerprint($"node|tx_seen_in_block|{TxId(i)}|block-{i / 3}")
                ),
                cancellationToken
            );
        }

        var sw = Stopwatch.StartNew();
        var replay = await journal.ReadAsync(JournalSequence.Empty, scenario.ObservationCount, cancellationToken);
        sw.Stop();

        return new JournalBenchmarkMetrics(
            $"{scenario.Name}:replay",
            replay.Count,
            0,
            0,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(replay.Count, sw.ElapsedMilliseconds),
            replay.Count == 0 ? 0 : replay[^1].Sequence.Value
        );
    }

    public async Task<JournalBenchmarkMetrics> MeasureDuplicateAsync(
        JournalBenchmarkScenario scenario,
        CancellationToken cancellationToken = default
    )
    {
        using var store = GetDocumentStore();
        var journal = new RavenObservationJournal<JournalObservation>(store);
        var request = new ObservationJournalAppendRequest<ObservationJournalEntry<JournalObservation>>(
            new ObservationJournalEntry<JournalObservation>(
                new JournalObservation("tx_seen_in_mempool", "tx-duplicate", null, null, "node")
            ),
            new DedupeFingerprint("node|tx_seen_in_mempool|tx-duplicate")
        );

        var sw = Stopwatch.StartNew();
        var duplicatesDetected = 0;
        var lastSequence = JournalSequence.Empty;

        for (var i = 0; i < scenario.DuplicateAttempts; i++)
        {
            var result = await journal.AppendAsync(request, cancellationToken);
            if (result.IsDuplicate)
                duplicatesDetected++;

            lastSequence = result.Sequence;
        }

        sw.Stop();

        return new JournalBenchmarkMetrics(
            $"{scenario.Name}:duplicate",
            1,
            scenario.DuplicateAttempts,
            duplicatesDetected,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.DuplicateAttempts, sw.ElapsedMilliseconds),
            lastSequence.Value
        );
    }

    private static double ToThroughputPerSecond(int operations, long elapsedMilliseconds)
        => elapsedMilliseconds <= 0
            ? operations
            : operations * 1000.0 / elapsedMilliseconds;

    private static string TxId(int index) => index.ToString("x64");

    private static string PayloadHex(int index) => $"01000000{index:x8}";

    private sealed record JournalObservation(
        string EventType,
        string EntityId,
        string? BlockHash,
        int? BlockHeight,
        string Source
    );
}
