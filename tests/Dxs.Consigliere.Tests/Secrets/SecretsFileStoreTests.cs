using System.Runtime.InteropServices;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Data.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Secrets;

/// <summary>
/// wave-A3 S5 — file-only behaviour pins for SecretsFileStore.
/// Migration coverage lives in SecretsFileStoreMigrationTests
/// (RavenTestDriver-backed).
/// </summary>
public sealed class SecretsFileStoreTests : IDisposable
{
    private readonly string _tmpDir;

    public SecretsFileStoreTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "consigliere-secrets-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tmpDir))
            Directory.Delete(_tmpDir, recursive: true);
    }

    [Fact]
    public async Task GetAsync_returns_null_when_file_does_not_exist()
    {
        var store = BuildStore();
        Assert.Null(await store.GetAsync());
    }

    [Fact]
    public async Task SaveAsync_round_trips_the_document()
    {
        var store = BuildStore();
        var doc = new RealtimeSourcePolicyOverrideDocument
        {
            Id = RealtimeSourcePolicyOverrideDocument.DocumentId,
            PrimaryRealtimeSource = "bitails",
            RawTxPrimaryProvider = "junglebus",
            RestPrimaryProvider = "whatsonchain",
            BitailsTransport = "websocket",
            BitailsApiKey = "secret-bitails-api-key",
            BitailsBaseUrl = "https://api.bitails.io",
            WhatsonchainApiKey = "secret-woc-key",
            JungleBusBaseUrl = "https://junglebus.gorillapool.io",
            UpdatedBy = "test",
        };

        await store.SaveAsync(doc);
        var loaded = await store.GetAsync();

        Assert.NotNull(loaded);
        Assert.Equal("bitails", loaded!.PrimaryRealtimeSource);
        Assert.Equal("secret-bitails-api-key", loaded.BitailsApiKey);
        Assert.Equal("secret-woc-key", loaded.WhatsonchainApiKey);
        Assert.Equal("https://junglebus.gorillapool.io", loaded.JungleBusBaseUrl);
    }

    [Fact]
    public async Task SaveAsync_writes_file_with_owner_read_write_only_on_posix()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var store = BuildStore();
        var doc = new RealtimeSourcePolicyOverrideDocument { Id = RealtimeSourcePolicyOverrideDocument.DocumentId, UpdatedBy = "t" };
        await store.SaveAsync(doc);

        var mode = File.GetUnixFileMode(store.FilePath);
        // Owner-rw only — no group / other access. Chmod 600.
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    [Fact]
    public async Task ResetAsync_removes_the_file()
    {
        var store = BuildStore();
        await store.SaveAsync(new RealtimeSourcePolicyOverrideDocument
        {
            Id = RealtimeSourcePolicyOverrideDocument.DocumentId,
            UpdatedBy = "test",
        });
        Assert.True(File.Exists(store.FilePath));

        await store.ResetAsync();
        Assert.False(File.Exists(store.FilePath));
    }

    [Fact]
    public async Task UpsertAsync_seeds_a_fresh_document_when_no_file_exists()
    {
        var store = BuildStore();
        var doc = await store.UpsertAsync("junglebus", "zmq", "alice");

        Assert.Equal("junglebus", doc.PrimaryRealtimeSource);
        Assert.Equal("zmq", doc.BitailsTransport);
        Assert.Equal("alice", doc.UpdatedBy);

        var loaded = await store.GetAsync();
        Assert.Equal("junglebus", loaded!.PrimaryRealtimeSource);
    }

    [Fact]
    public async Task UpsertAsync_preserves_other_fields_on_existing_file()
    {
        // The interface's UpsertAsync only updates the two
        // policy switches + updatedBy. API keys + URLs that
        // came from an earlier SaveAsync must survive.
        var store = BuildStore();
        await store.SaveAsync(new RealtimeSourcePolicyOverrideDocument
        {
            Id = RealtimeSourcePolicyOverrideDocument.DocumentId,
            PrimaryRealtimeSource = "bitails",
            BitailsTransport = "websocket",
            BitailsApiKey = "still-here",
            WhatsonchainBaseUrl = "https://whatsonchain.example",
            UpdatedBy = "first",
        });

        var upserted = await store.UpsertAsync("junglebus", "zmq", "second");

        Assert.Equal("still-here", upserted.BitailsApiKey);
        Assert.Equal("https://whatsonchain.example", upserted.WhatsonchainBaseUrl);
        Assert.Equal("junglebus", upserted.PrimaryRealtimeSource);
        Assert.Equal("zmq", upserted.BitailsTransport);
        Assert.Equal("second", upserted.UpdatedBy);
    }

    private SecretsFileStore BuildStore()
    {
        var options = Options.Create(new ConsigliereSecretsConfig { Dir = _tmpDir });
        var env = new StubHostEnvironment(_tmpDir);
        return new SecretsFileStore(options, env, NullLogger<SecretsFileStore>.Instance);
    }

    private sealed class StubHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Dxs.Consigliere.Tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
