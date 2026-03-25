using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Models.Transactions;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data;

public class RavenRawTransactionPayloadStoreIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task SaveAsync_StoresPayloadOnceAndLoadsByTxId()
    {
        if (!HasDotNet8Runtime())
            return;

        using var store = GetDocumentStore();
        var sut = new RavenRawTransactionPayloadStore(store);

        const string txId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string payload = "0100000001feedface";

        var reference = await sut.SaveAsync(txId, payload);
        var loaded = await sut.LoadByTxIdAsync(txId);

        Assert.NotNull(loaded);
        Assert.Equal(reference, loaded!.Reference);
        Assert.Equal(payload, loaded.PayloadHex);

        using var session = store.OpenSession();
        var count = session.Query<RawTransactionPayloadDocument>().Count();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SaveAsync_ReusesExistingPayloadForIdenticalWrites()
    {
        if (!HasDotNet8Runtime())
            return;

        using var store = GetDocumentStore();
        var sut = new RavenRawTransactionPayloadStore(store);

        const string txId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string payload = "0200000002cafebabe";

        var first = await sut.SaveAsync(txId, payload, RawTransactionPayloadCompressionAlgorithm.Gzip);
        var second = await sut.SaveAsync(txId, payload, RawTransactionPayloadCompressionAlgorithm.Gzip);

        Assert.Equal(first, second);

        using var session = store.OpenSession();
        var count = session.Query<RawTransactionPayloadDocument>().Count();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SaveAsync_RejectsConflictingPayloadForExistingTxId()
    {
        if (!HasDotNet8Runtime())
            return;

        using var store = GetDocumentStore();
        var sut = new RavenRawTransactionPayloadStore(store);

        const string txId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

        await sut.SaveAsync(txId, "0300000003deadbeef");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SaveAsync(txId, "0400000004b16b00b5")
        );

        Assert.Contains(txId, exception.Message);
    }

    private static bool HasDotNet8Runtime()
    {
        var dotnetPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrWhiteSpace(dotnetPath))
            dotnetPath = "dotnet";

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = dotnetPath,
                Arguments = "--list-runtimes",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output.Contains("Microsoft.NETCore.App 8.", StringComparison.Ordinal);
    }
}
