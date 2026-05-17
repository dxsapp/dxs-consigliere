// Wave 1 S7 — Headers chain soak recorder.
//
// Drives the production HeadersChainService path (audit A2 H2 fix):
// real PeerManager + real HeadersChainService + in-memory
// IBlockHeaderStore + JSONL-capturing INewBlockNotifier + the actual
// WhatsOnChain bootstrap source. At end of soak queries
// store.GetTipAsync() (same call AdminP2pController.HeadersTip uses).
//
// JSONL schema (each line one JSON object), per slices.md §S7:
//   { type: "p2p"|"woc"|"http_error"|"decode_error",
//     ts_utc_ms: int64,
//     height: int64|null,
//     tip_hash: string|null,             // display order, lowercase hex
//     prev_hash: string|null,
//     header_timestamp_ms: int64|null,
//     source_seq: int64,
//     extra: object|null }
//
// Audit A2 H3: tip_hash is display-order (matches WhatsOnChain). Joins
// against woc.bestblockhash are byte-equal without manual reversal.
//
// Run:
//   dotnet run --project tests/Spikes/P2p/HeadersSoakRecorder
//
// On a VPS soak run, ensure NTP is enabled (chrony / systemd-timesyncd,
// offset < 50 ms) before starting.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Pool;
using Dxs.Bsv.P2p.Session;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.P2p;
using Dxs.Consigliere.WebSockets;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// ── Config ─────────────────────────────────────────────────────────────
var soakMinutes        = GetInt("SOAK_MINUTES",      1440); // 24h default
var poolSize           = GetInt("POOL_SIZE",         8);
var wocPollIntervalSec = GetInt("WOC_POLL_INTERVAL_SEC", 1);
var outputDir          = Env("OUTPUT_DIR",           ".");
var enableFallback     = Env("ENABLE_FALLBACK",      "true") == "true";
var sessionId          = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
var jsonlPath          = Path.Combine(outputDir, $"headers-soak-{sessionId}.jsonl");

Directory.CreateDirectory(outputDir);
Console.WriteLine($"HeadersSoakRecorder starting. soak={soakMinutes}min pool={poolSize} out={jsonlPath}");

// ── Output writer (thread-safe append) ─────────────────────────────────
var writeLock = new object();
long p2pSeq = 0, wocSeq = 0, errSeq = 0;

void Emit(string type, long? height, string? tipHash, string? prevHash, long? headerTimestampMs, long seq, JsonObject? extra = null)
{
    var rec = new JsonObject
    {
        ["type"] = type,
        ["ts_utc_ms"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        ["height"] = height,
        ["tip_hash"] = tipHash,
        ["prev_hash"] = prevHash,
        ["header_timestamp_ms"] = headerTimestampMs,
        ["source_seq"] = seq,
        ["extra"] = extra,
    };
    var line = rec.ToJsonString();
    lock (writeLock) File.AppendAllText(jsonlPath, line + "\n");
}

// ── P2P pool ──────────────────────────────────────────────────────────
var network = P2pNetwork.Mainnet;
var peerStore = new InMemoryPeerStore();
var discovery = new PeerDiscovery(network, peerStore);
var pmConfig = new PeerManagerConfig
{
    TargetPoolSize = poolSize,
    BootstrapMaxConcurrency = 4,
    BootstrapJitter = TimeSpan.FromMilliseconds(200),
    NegativeCooldown = TimeSpan.FromMinutes(15),
    MaintenanceInterval = TimeSpan.FromSeconds(15),
    DnsRefreshInterval = TimeSpan.FromHours(6),
    EnableFallbackSeeds = enableFallback,
    VersionFactory = BuildVersion,
    SessionConfig = new PeerSessionConfig { SendProtoconfAfterVerack = true },
};
var manager = new PeerManager(network, discovery, peerStore, pmConfig);
await manager.StartAsync(CancellationToken.None);
var health = new BsvP2pHealth();
health.Bind(manager, peerStore);

// ── Production headers stack ──────────────────────────────────────────
var headersOptions = Options.Create(new HeadersChainOptions
{
    RetainedHeaderCount = 200,
    GetHeadersIntervalMs = 30_000,
    SeedFromBitails = true,
    BootstrapTimeoutMs = 10_000,
});
var chain = new HeadersChain(headersOptions.Value);
var inMemoryStore = new InMemoryBlockHeaderStore();

// JSONL-capturing notifier — sits where HubNewBlockNotifier sits in
// production; same interface, no SignalR.
var notifier = new JsonlEmittingNotifier((tip) =>
{
    Emit("p2p", tip.Height, tip.Hash, tip.PrevHash, tip.TimestampMs,
        Interlocked.Increment(ref p2pSeq));
});

var bootstrapLogger = NullLogger<WhatsOnChainHeadersBootstrapSource>.Instance;
var bootstrapSource = new WhatsOnChainHeadersBootstrapSource(bootstrapLogger);
var bootstrapper = new HeadersChainBootstrapper(
    chain,
    inMemoryStore,
    bootstrapSource,
    headersOptions,
    NullLogger<HeadersChainBootstrapper>.Instance);

var service = new HeadersChainService(
    health,
    chain,
    headersOptions,
    inMemoryStore,
    notifier,
    bootstrapper,
    Options.Create(new BsvP2pConfig { Enabled = true }),
    NullLogger<HeadersChainService>.Instance);

await service.StartAsync(CancellationToken.None);

// ── WhatsOnChain poll side ─────────────────────────────────────────────
using var http = new HttpClient { BaseAddress = new Uri("https://api.whatsonchain.com/") };
http.Timeout = TimeSpan.FromSeconds(10);
var wocCts = new CancellationTokenSource();
var lastWocHeight = -1L;
var wocLoop = Task.Run(async () =>
{
    while (!wocCts.IsCancellationRequested)
    {
        try
        {
            using var resp = await http.GetAsync("v1/bsv/main/chain/info", wocCts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                Emit("http_error", null, null, null, null, Interlocked.Increment(ref errSeq),
                    new JsonObject { ["status"] = (int)resp.StatusCode, ["url"] = "v1/bsv/main/chain/info" });
            }
            else
            {
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: wocCts.Token);
                var height = json.TryGetProperty("blocks", out var bp) ? bp.GetInt64() : -1;
                var tipHash = json.TryGetProperty("bestblockhash", out var bh) ? bh.GetString() : null;
                if (height >= 0 && height != lastWocHeight && tipHash is not null)
                {
                    lastWocHeight = height;
                    // WoC returns display-order; our p2p emits are also display-order
                    // (audit A2 H3) so joins are byte-equal without any conversion.
                    Emit("woc", height, tipHash.ToLowerInvariant(), null, null, Interlocked.Increment(ref wocSeq));
                }
            }
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex)
        {
            Emit("http_error", null, null, null, null, Interlocked.Increment(ref errSeq),
                new JsonObject { ["reason"] = ex.Message });
        }

        try { await Task.Delay(TimeSpan.FromSeconds(wocPollIntervalSec), wocCts.Token); }
        catch { break; }
    }
});

// ── Reconcile loop — drive HeadersChainService.Reconcile periodically ──
var reconcileCts = new CancellationTokenSource();
var reconcileLoop = Task.Run(async () =>
{
    while (!reconcileCts.IsCancellationRequested)
    {
        try { service.Reconcile(); } catch { /* swallow */ }
        try { await Task.Delay(TimeSpan.FromSeconds(5), reconcileCts.Token); } catch { break; }
    }
});

// ── Run for soakMinutes ────────────────────────────────────────────────
Console.WriteLine($"Soak will run for {soakMinutes} minutes; press Ctrl+C to stop early.");
var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(soakMinutes));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopCts.Cancel(); };
try { await Task.Delay(Timeout.InfiniteTimeSpan, stopCts.Token); } catch (OperationCanceledException) { }

// ── End-of-soak: query store tip (same call AdminP2pController makes) ──
var finalTip = await inMemoryStore.GetTipAsync(CancellationToken.None);
if (finalTip is not null)
{
    Console.WriteLine($"End-of-soak tip: height={finalTip.Height} hash={BlockHeaderHasher.ToDisplayHex(Convert.FromHexString(finalTip.Hash))}");
}
else
{
    Console.WriteLine("End-of-soak tip: store empty (chain never anchored)");
}

Console.WriteLine("Stopping soak…");
wocCts.Cancel();
reconcileCts.Cancel();
try { await wocLoop; } catch { }
try { await reconcileLoop; } catch { }
await service.StopAsync(CancellationToken.None);
await manager.DisposeAsync();
Console.WriteLine($"Done. JSONL written to {jsonlPath}");

return;

// ── Helpers ────────────────────────────────────────────────────────────
static int GetInt(string name, int def)
    => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : def;

static string Env(string name, string def)
    => Environment.GetEnvironmentVariable(name) ?? def;

static VersionMessage BuildVersion() => new(
    ProtocolVersion: 70016,
    Services: 0x25,
    TimestampUnixSeconds: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
    AddrRecv: P2pAddress.Anonymous(0x25),
    AddrFrom: P2pAddress.Anonymous(0x25),
    Nonce: (ulong)Random.Shared.NextInt64(),
    UserAgent: "/Bitcoin SV:1.2.1/",
    StartHeight: 0,
    Relay: true,
    AssociationId: null);

/// <summary>
/// Thread-safe in-memory <see cref="IBlockHeaderStore"/> for the spike.
/// The production path uses Raven-backed BlockHeaderStore; the spike
/// substitutes this to avoid pulling in embedded Raven.
/// </summary>
file sealed class InMemoryBlockHeaderStore : IBlockHeaderStore
{
    private readonly ConcurrentDictionary<string, BlockHeaderDocument> _byHash = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(BlockHeaderDocument doc, CancellationToken ct = default)
    {
        _byHash[doc.Hash] = doc;
        return Task.CompletedTask;
    }

    public Task<BlockHeaderDocument> GetByHashAsync(string hashHex, CancellationToken ct = default)
        => Task.FromResult(_byHash.TryGetValue(hashHex, out var doc) ? doc : null!);

    public Task<BlockHeaderDocument> GetTipAsync(CancellationToken ct = default)
    {
        BlockHeaderDocument? tip = null;
        foreach (var doc in _byHash.Values)
        {
            if (tip is null || doc.Height > tip.Height) tip = doc;
        }
        return Task.FromResult(tip!);
    }

    public Task<IReadOnlyList<BlockHeaderDocument>> RecentAsync(int count, CancellationToken ct = default)
    {
        if (count <= 0) return Task.FromResult<IReadOnlyList<BlockHeaderDocument>>(Array.Empty<BlockHeaderDocument>());
        var sorted = new List<BlockHeaderDocument>(_byHash.Values);
        sorted.Sort((a, b) => b.Height.CompareTo(a.Height));
        IReadOnlyList<BlockHeaderDocument> top = sorted.GetRange(0, Math.Min(count, sorted.Count));
        return Task.FromResult(top);
    }

    public Task PruneBelowAsync(long minHeight, CancellationToken ct = default)
    {
        var stale = new List<string>();
        foreach (var (k, v) in _byHash) if (v.Height < minHeight) stale.Add(k);
        foreach (var k in stale) _byHash.TryRemove(k, out _);
        return Task.CompletedTask;
    }

    // W3 A2-followup N1: persistent active-tip pointer. Spike doesn't
    // exercise restart-tip semantics — minimal in-memory backing field.
    // Nullable per the interface signature (A2-followup-3 spike-
    // nullability note).
    private BlockHeaderActiveTip? _activeTip;
    public Task SetActiveTipAsync(string blockHashHex, long height, CancellationToken ct = default)
    {
        _activeTip = new BlockHeaderActiveTip(blockHashHex, height);
        return Task.CompletedTask;
    }
    public Task<BlockHeaderActiveTip?> GetActiveTipAsync(CancellationToken ct = default)
        => Task.FromResult(_activeTip);
}

file sealed class JsonlEmittingNotifier(Action<BlockTipDto> onTip) : INewBlockNotifier
{
    public Task NotifyAsync(BlockTipDto tip, CancellationToken ct)
    {
        onTip(tip);
        return Task.CompletedTask;
    }
}
