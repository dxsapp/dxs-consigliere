namespace Dxs.Consigliere.Benchmarks.Storage;

public class StorageGrowthBenchmarkSmokeTests
{
    [Fact]
    public async Task MeasuresJournalOnlyAndPayloadBackedStorageGrowth()
    {
        var harness = new StorageGrowthBenchmarkHarness();

        var journalOnly = await harness.MeasureStorageGrowthAsync(
            new StorageGrowthBenchmarkScenario("storage-journal-only", 16, 256, PersistPayloads: false));
        var payloadBacked = await harness.MeasureStorageGrowthAsync(
            new StorageGrowthBenchmarkScenario("storage-payload-backed", 16, 256, PersistPayloads: true));

        Assert.Equal(16, journalOnly.JournalDocumentCount);
        Assert.Equal(0, journalOnly.PayloadDocumentCount);
        Assert.Equal(16, payloadBacked.JournalDocumentCount);
        Assert.Equal(16, payloadBacked.PayloadDocumentCount);
        Assert.True(payloadBacked.PayloadDocumentBytes > 0);
        Assert.True(payloadBacked.ThroughputPerSecond > 0);
    }
}
