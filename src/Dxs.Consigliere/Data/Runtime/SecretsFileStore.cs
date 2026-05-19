using System.Runtime.InteropServices;
using System.Text.Json;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;

namespace Dxs.Consigliere.Data.Runtime;

/// <summary>
/// wave-A3 S5 — on-disk replacement for the Raven-backed
/// realtime-source-policy override store. The provider-config
/// document previously lived inside RavenDB with plaintext API
/// keys; S5 moves it onto a chmod-600 JSON file under
/// <c>Consigliere:Secrets:Dir</c> so a Raven dump never leaks
/// credentials. The wave-A2 Raven document is migrated once on
/// first wave-A3 startup (see <see cref="MigrateFromRavenAsync"/>)
/// and then deleted — there is no parallel path or back-compat
/// shim. Both producer (this store) and consumer
/// (<c>AdminProviderConfigService</c>) live in-repo.
/// </summary>
public sealed class SecretsFileStore : IRealtimeSourcePolicyOverrideStore
{
    private const string FileName = "providers.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly ILogger<SecretsFileStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SecretsFileStore(
        IOptions<ConsigliereSecretsConfig> options,
        IHostEnvironment hostEnvironment,
        ILogger<SecretsFileStore> logger)
    {
        _logger = logger;
        var configured = options.Value.Dir;
        var resolved = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(hostEnvironment.ContentRootPath, configured);
        _filePath = Path.Combine(resolved, FileName);
    }

    /// <summary>Full path the store reads + writes from. Used by tests.</summary>
    internal string FilePath => _filePath;

    public async Task<RealtimeSourcePolicyOverrideDocument> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var stream = File.OpenRead(_filePath);
            var doc = await JsonSerializer.DeserializeAsync<RealtimeSourcePolicyOverrideDocument>(stream, JsonOptions, cancellationToken);
            return doc;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RealtimeSourcePolicyOverrideDocument> SaveAsync(
        RealtimeSourcePolicyOverrideDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Id = RealtimeSourcePolicyOverrideDocument.DocumentId;
        document.SetUpdate();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await WriteFileAsync(document, cancellationToken);
            return document;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RealtimeSourcePolicyOverrideDocument> UpsertAsync(
        string primaryRealtimeSource,
        string bitailsTransport,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var existing = File.Exists(_filePath)
                ? await ReadAsync(cancellationToken)
                : new RealtimeSourcePolicyOverrideDocument { Id = RealtimeSourcePolicyOverrideDocument.DocumentId };

            existing.PrimaryRealtimeSource = primaryRealtimeSource;
            existing.BitailsTransport = bitailsTransport;
            existing.UpdatedBy = updatedBy;
            existing.SetUpdate();

            await WriteFileAsync(existing, cancellationToken);
            return existing;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// wave-A3 S5 migration: import the wave-A2 Raven document
    /// onto the on-disk secrets store and delete the Raven copy.
    ///
    /// S5-audit M1 fix: previous revision short-circuited on
    /// <c>File.Exists</c>, which meant a startup that
    /// successfully wrote the file but failed the Raven delete
    /// would never retry — the plaintext secrets would stay in
    /// RavenDB forever. The probe now keys on Raven instead: as
    /// long as a Raven document exists we keep deleting it, and
    /// only write the file if it isn't already authoritative.
    ///
    /// Runs at startup. Fail-stop semantics — any IO error
    /// throws so the host refuses to start until the operator
    /// resolves it. Idempotent: a clean run with no Raven doc
    /// and an existing file is a no-op.
    /// </summary>
    public async Task MigrateFromRavenAsync(IDocumentStore documentStore, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var session = documentStore.OpenAsyncSession();
            var ravenDoc = await session.LoadAsync<RealtimeSourcePolicyOverrideDocument>(
                RealtimeSourcePolicyOverrideDocument.DocumentId, cancellationToken);

            if (ravenDoc is null)
            {
                _logger.LogDebug("[secrets] no wave-A2 Raven provider-config document; nothing to migrate");
                return;
            }

            if (!File.Exists(_filePath))
            {
                // The file isn't authoritative yet — seed it
                // from the Raven payload before deleting the
                // Raven copy so we never have a window where
                // neither location holds the secrets.
                ravenDoc.Id = RealtimeSourcePolicyOverrideDocument.DocumentId;
                await WriteFileAsync(ravenDoc, cancellationToken);
                _logger.LogInformation("[secrets] migrated wave-A2 provider-config document from Raven to {Path}", _filePath);
            }
            else
            {
                _logger.LogInformation("[secrets] file at {Path} already authoritative; deleting stale Raven document", _filePath);
            }

            // Always delete the Raven document if it exists.
            // The file is authoritative; the Raven copy is now
            // either redundant (just-copied) or stale (a prior
            // delete attempt failed) — either way it must not
            // outlive this call.
            session.Delete(ravenDoc);
            await session.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RealtimeSourcePolicyOverrideDocument> ReadAsync(CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<RealtimeSourcePolicyOverrideDocument>(stream, JsonOptions, cancellationToken)
               ?? new RealtimeSourcePolicyOverrideDocument { Id = RealtimeSourcePolicyOverrideDocument.DocumentId };
    }

    private async Task WriteFileAsync(RealtimeSourcePolicyOverrideDocument document, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
        }

        // chmod 600 on POSIX BEFORE replacing — owner-rw only.
        // Docker secrets mount 444 by default and refuse mode
        // changes; in that case we accept the existing mode.
        SetOwnerReadWrite(tempPath);

        File.Move(tempPath, _filePath, overwrite: true);
        SetOwnerReadWrite(_filePath);
    }

    private static void SetOwnerReadWrite(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (UnauthorizedAccessException)
        {
            // Docker secrets mounts (read-only 444) cannot be
            // re-chmod'd. The mount itself is already as
            // restrictive as we'd want, so swallow the error.
        }
        catch (IOException)
        {
            // Same as above for tmpfs-style mounts.
        }
    }
}
