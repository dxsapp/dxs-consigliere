using System.Globalization;
using System.Text;

using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Benchmarks.Storage;

public class StorageGrowthBenchmarkEvidenceTests
{
    private static readonly string EvidencePath = RepoPathResolver.ResolveFromRepoRoot(
        "doc",
        "stream-tasks",
        "consigliere-vnext",
        "benchmarks",
        "B5-storage-growth-benchmarks-evidence.md");

    [Fact]
    public async Task WritesBenchmarkEvidenceSnapshot()
    {
        var harness = new StorageGrowthBenchmarkHarness();
        var journalOnlyScenario = new StorageGrowthBenchmarkScenario("storage-journal-only", 32, 512, PersistPayloads: false);
        var payloadBackedScenario = new StorageGrowthBenchmarkScenario("storage-payload-backed", 32, 2048, PersistPayloads: true);

        var journalOnly = await harness.MeasureStorageGrowthAsync(journalOnlyScenario);
        var payloadBacked = await harness.MeasureStorageGrowthAsync(payloadBackedScenario);

        var markdown = new StringBuilder()
            .AppendLine("# B5 Storage Growth And Payload Economics Evidence")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- journal_only_tx_observations: {journalOnly.TxObservations}")
            .AppendLine($"- journal_only_raw_transaction_bytes: {journalOnly.RawTransactionBytes}")
            .AppendLine($"- journal_only_journal_documents: {journalOnly.JournalDocumentCount}")
            .AppendLine($"- journal_only_journal_document_bytes: {journalOnly.JournalDocumentBytes}")
            .AppendLine($"- journal_only_observation_json_bytes: {journalOnly.JournalObservationJsonBytes}")
            .AppendLine($"- journal_only_elapsed_ms: {journalOnly.ElapsedMilliseconds}")
            .AppendLine($"- journal_only_throughput_per_sec: {journalOnly.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- payload_backed_tx_observations: {payloadBacked.TxObservations}")
            .AppendLine($"- payload_backed_raw_transaction_bytes: {payloadBacked.RawTransactionBytes}")
            .AppendLine($"- payload_backed_journal_documents: {payloadBacked.JournalDocumentCount}")
            .AppendLine($"- payload_backed_payload_documents: {payloadBacked.PayloadDocumentCount}")
            .AppendLine($"- payload_backed_journal_document_bytes: {payloadBacked.JournalDocumentBytes}")
            .AppendLine($"- payload_backed_observation_json_bytes: {payloadBacked.JournalObservationJsonBytes}")
            .AppendLine($"- payload_backed_payload_hex_bytes: {payloadBacked.PayloadHexBytes}")
            .AppendLine($"- payload_backed_payload_document_bytes: {payloadBacked.PayloadDocumentBytes}")
            .AppendLine($"- payload_backed_elapsed_ms: {payloadBacked.ElapsedMilliseconds}")
            .AppendLine($"- payload_backed_throughput_per_sec: {payloadBacked.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine()
            .AppendLine("- note: payload growth is measured against the current Raven payload backend, which stores raw hex and does not yet provide true compressed-at-rest savings.")
            .AppendLine("- note: this benchmark is meant to expose storage economics trends, not to certify final archival efficiency.")
            .AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        await File.WriteAllTextAsync(EvidencePath, markdown.ToString());

        Assert.True(journalOnly.JournalDocumentBytes > 0);
        Assert.True(payloadBacked.PayloadDocumentBytes > payloadBacked.JournalDocumentBytes / 2);
    }
}
