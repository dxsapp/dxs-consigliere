using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.Benchmarks.Shared;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Journal;
using Dxs.Consigliere.Data.Models.Transactions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Benchmarks.Storage;

public sealed class StorageGrowthBenchmarkHarness : ConfiguredRavenBenchmarkTestDriver
{
    public async Task<StorageGrowthBenchmarkMetrics> MeasureStorageGrowthAsync(
        StorageGrowthBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        var writer = CreateTxWriter(store, scenario.PersistPayloads);

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < scenario.TxObservationCount; i++)
        {
            var txId = VNextBenchmarkFixtureFactory.TransferTxId(i);
            var transaction = VNextBenchmarkFixtureFactory.CreateRuntimeTransaction(
                txId,
                [VNextBenchmarkFixtureFactory.TokenId(i)],
                scenario.RawTransactionBytes);

            await writer.AppendAsync(
                TxMessage.AddedToMempool(transaction, 1_710_500_000 + i, TxObservationSource.Node),
                cancellationToken);
        }
        sw.Stop();

        using var session = store.OpenAsyncSession();
        var journalDocuments = await session.Advanced.AsyncDocumentQuery<ObservationJournalRecordDocument>()
            .WaitForNonStaleResults()
            .ToListAsync(token: cancellationToken);
        var payloadDocuments = await session.Advanced.AsyncDocumentQuery<RawTransactionPayloadDocument>()
            .WaitForNonStaleResults()
            .ToListAsync(token: cancellationToken);

        var journalObservationJsonBytes = journalDocuments.Sum(x => Encoding.UTF8.GetByteCount(x.ObservationJson ?? string.Empty));
        var journalDocumentBytes = journalDocuments.Sum(GetSerializedSize);
        var payloadHexBytes = payloadDocuments.Sum(x => Encoding.UTF8.GetByteCount(x.PayloadHex ?? string.Empty));
        var payloadDocumentBytes = payloadDocuments.Sum(GetSerializedSize);

        return new StorageGrowthBenchmarkMetrics(
            scenario.Name,
            scenario.TxObservationCount,
            scenario.RawTransactionBytes,
            scenario.PersistPayloads,
            journalDocuments.Count,
            payloadDocuments.Count,
            journalObservationJsonBytes,
            journalDocumentBytes,
            payloadHexBytes,
            payloadDocumentBytes,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.TxObservationCount, sw.ElapsedMilliseconds));
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

    private static int GetSerializedSize<T>(T document)
        => Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(document));

    private static double ToThroughputPerSecond(int operations, long elapsedMilliseconds)
        => elapsedMilliseconds <= 0
            ? operations
            : operations * 1000.0 / elapsedMilliseconds;
}
