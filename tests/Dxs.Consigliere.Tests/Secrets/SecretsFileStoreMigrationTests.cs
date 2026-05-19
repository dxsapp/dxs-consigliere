using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Tests.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Secrets;

/// <summary>
/// wave-A3 S5 — round-trip the wave-A2 Raven provider-config
/// document through <see cref="SecretsFileStore.MigrateFromRavenAsync"/>
/// and verify (a) the file lands with the same content, (b) the
/// Raven document is gone, (c) re-running the migration is a
/// no-op.
/// </summary>
public sealed class SecretsFileStoreMigrationTests : RavenTestDriver, IDisposable
{
    static SecretsFileStoreMigrationTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }

    private readonly string _tmpDir;

    public SecretsFileStoreMigrationTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "consigliere-secrets-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
    }

    public new void Dispose()
    {
        base.Dispose();
        if (Directory.Exists(_tmpDir))
            Directory.Delete(_tmpDir, recursive: true);
    }

    [SkippableFact]
    public async Task MigrateFromRavenAsync_copies_doc_to_file_and_deletes_from_raven()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8));

        using var documentStore = GetDocumentStore();
        await SeedRavenDocAsync(documentStore, new RealtimeSourcePolicyOverrideDocument
        {
            Id = RealtimeSourcePolicyOverrideDocument.DocumentId,
            PrimaryRealtimeSource = "bitails",
            RawTxPrimaryProvider = "junglebus",
            RestPrimaryProvider = "whatsonchain",
            BitailsTransport = "websocket",
            BitailsApiKey = "leaked-from-raven",
            BitailsBaseUrl = "https://api.bitails.io",
            JungleBusBaseUrl = "https://junglebus.gorillapool.io",
            UpdatedBy = "wave-A2-install",
        });

        var fileStore = BuildStore();
        await fileStore.MigrateFromRavenAsync(documentStore);

        // The file now mirrors the Raven document, including
        // the secret API key — that's the whole point: the
        // operator's existing data must survive the cutover.
        var loaded = await fileStore.GetAsync();
        Assert.NotNull(loaded);
        Assert.Equal("bitails", loaded!.PrimaryRealtimeSource);
        Assert.Equal("leaked-from-raven", loaded.BitailsApiKey);

        // Raven document is gone.
        using var session = documentStore.OpenAsyncSession();
        var remaining = await session.LoadAsync<RealtimeSourcePolicyOverrideDocument>(
            RealtimeSourcePolicyOverrideDocument.DocumentId);
        Assert.Null(remaining);
    }

    [SkippableFact]
    public async Task MigrateFromRavenAsync_keeps_file_authoritative_AND_deletes_stale_Raven()
    {
        // S5-audit M1: an earlier revision short-circuited on
        // File.Exists, which left plaintext provider secrets
        // in Raven forever any time a prior startup's
        // Raven-delete failed. The new contract: the file
        // payload wins (operator may have edited it via the
        // admin UI after a previous migrate-then-crash), but
        // the Raven document MUST get cleaned up regardless.
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8));

        var fileStore = BuildStore();
        await fileStore.SaveAsync(new RealtimeSourcePolicyOverrideDocument
        {
            Id = RealtimeSourcePolicyOverrideDocument.DocumentId,
            PrimaryRealtimeSource = "junglebus",
            BitailsApiKey = "file-wins",
            UpdatedBy = "operator-edited-file",
        });

        using var documentStore = GetDocumentStore();
        await SeedRavenDocAsync(documentStore, new RealtimeSourcePolicyOverrideDocument
        {
            Id = RealtimeSourcePolicyOverrideDocument.DocumentId,
            PrimaryRealtimeSource = "bitails",
            BitailsApiKey = "raven-stale",
            UpdatedBy = "wave-A2-install",
        });

        await fileStore.MigrateFromRavenAsync(documentStore);

        // File content is preserved; Raven copy is removed.
        var loaded = await fileStore.GetAsync();
        Assert.Equal("junglebus", loaded!.PrimaryRealtimeSource);
        Assert.Equal("file-wins", loaded.BitailsApiKey);

        using var session = documentStore.OpenAsyncSession();
        var raven = await session.LoadAsync<RealtimeSourcePolicyOverrideDocument>(
            RealtimeSourcePolicyOverrideDocument.DocumentId);
        Assert.Null(raven);
    }

    [SkippableFact]
    public async Task MigrateFromRavenAsync_with_no_Raven_doc_and_existing_file_is_a_clean_noop()
    {
        // Once the migration has run once successfully there
        // is no Raven doc and the file is already authoritative.
        // Subsequent startups must be true no-ops — the file
        // content must NOT be rewritten and no Raven session
        // changes are flushed.
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8));

        var fileStore = BuildStore();
        await fileStore.SaveAsync(new RealtimeSourcePolicyOverrideDocument
        {
            Id = RealtimeSourcePolicyOverrideDocument.DocumentId,
            PrimaryRealtimeSource = "junglebus",
            BitailsApiKey = "file-only",
            UpdatedBy = "operator",
        });
        var before = File.GetLastWriteTimeUtc(fileStore.FilePath);
        await Task.Delay(20);

        using var documentStore = GetDocumentStore();
        await fileStore.MigrateFromRavenAsync(documentStore);

        var after = File.GetLastWriteTimeUtc(fileStore.FilePath);
        Assert.Equal(before, after);
    }

    [SkippableFact]
    public async Task MigrateFromRavenAsync_is_noop_when_no_raven_doc_exists()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8));

        using var documentStore = GetDocumentStore();
        var fileStore = BuildStore();

        await fileStore.MigrateFromRavenAsync(documentStore);

        Assert.False(File.Exists(fileStore.FilePath));
        Assert.Null(await fileStore.GetAsync());
    }

    private static async Task SeedRavenDocAsync(
        Raven.Client.Documents.IDocumentStore documentStore,
        RealtimeSourcePolicyOverrideDocument doc)
    {
        using var session = documentStore.OpenAsyncSession();
        await session.StoreAsync(doc, RealtimeSourcePolicyOverrideDocument.DocumentId);
        await session.SaveChangesAsync();
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
