// Wave 1 S7 — Headers chain soak recorder.
//
// Standalone console app that runs the BSV P2P peer pool + an in-process
// headers chain for >= 24h, records every observed-tip and every
// WhatsOnChain chain-info poll as JSONL, and exits when SOAK_MINUTES
// elapses. An offline script (analyze.fsx) joins the two streams by
// tip_hash and computes p50/p95/p99 lag per slices.md §S7 reproducibility
// rules.
//
// Run:
//   dotnet run --project tests/Spikes/P2p/HeadersSoakRecorder
//
// Required env:
//   none (defaults below); on a VPS run, ensure NTP is enabled and
//   chrony / systemd-timesyncd reports offset < 50 ms.
//
// JSONL schema (each line one JSON object):
//   { type: "p2p"|"woc"|"http_error"|"decode_error",
//     ts_utc_ms: int64,
//     height: int64|null,
//     tip_hash: string|null,         // lowercase hex, no 0x, wire order
//     prev_hash: string|null,
//     header_timestamp_ms: int64|null,
//     source_seq: int64,
//     extra: object|null }
//
// This file lives under tests/Spikes/* — NOT shipped in production
// artifacts.

using System;
using System.Collections.Concurrent;
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

// ── P2P side ───────────────────────────────────────────────────────────
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

// Hand-roll a tiny chain: detect new tips via OnHeadersReceived, dedupe
// by hash to avoid double-counting across peers.
var seenHashes = new ConcurrentDictionary<string, byte>();
long maxHeightSeen = 0;

void WireSession(PeerSession session)
{
    session.OnInvReceived = inv =>
    {
        foreach (var item in inv.Items)
        {
            if (item.Type != InvType.Block) continue;
            // Fire-and-forget getheaders; locator empty is fine for soak.
            try { _ = session.SendGetHeadersAsync(new GetHeadersMessage(70016, Array.Empty<byte[]>(), new byte[32]), CancellationToken.None); }
            catch { /* swallow */ }
        }
    };
    session.OnHeadersReceived = headers =>
    {
        foreach (var hdr in headers)
        {
            byte[] hash;
            try { hash = BlockHeaderHasher.Hash(hdr); }
            catch { continue; }
            var hashHex = Convert.ToHexString(hash).ToLowerInvariant();
            if (!seenHashes.TryAdd(hashHex, 1)) continue; // already recorded

            // We don't track height authoritatively here; assign monotonic
            // best-effort height by counting new hashes off the last
            // confirmed tip. The offline join uses tip_hash, not height,
            // so this is a soft hint only.
            var height = Interlocked.Increment(ref maxHeightSeen);
            var prevHex = Convert.ToHexString(BlockHeaderHasher.PrevBlock(hdr)).ToLowerInvariant();
            var headerTsMs = (long)BlockHeaderHasher.TimestampUnixSeconds(hdr) * 1000L;
            var seq = Interlocked.Increment(ref p2pSeq);
            Emit("p2p", height, hashHex, prevHex, headerTsMs, seq);
        }
    };
}

// Reconcile every 5 s — wire any new Ready sessions.
var reconcileCts = new CancellationTokenSource();
var reconcileLoop = Task.Run(async () =>
{
    var wired = new HashSet<PeerSession>(ReferenceEqualityComparer.Instance);
    while (!reconcileCts.IsCancellationRequested)
    {
        foreach (var s in manager.ActiveSessions.Values)
        {
            if (s.State == PeerSessionState.Ready && wired.Add(s)) WireSession(s);
        }
        try { await Task.Delay(TimeSpan.FromSeconds(5), reconcileCts.Token); }
        catch { break; }
    }
});

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
                    // WhatsOnChain returns display-order hash; reverse to wire-order
                    // for join compatibility with our P2P emits.
                    var wireHash = ReverseHex(tipHash);
                    Emit("woc", height, wireHash, null, null, Interlocked.Increment(ref wocSeq));
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

// ── Run for soakMinutes ────────────────────────────────────────────────
Console.WriteLine($"Soak will run for {soakMinutes} minutes; press Ctrl+C to stop early.");
var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(soakMinutes));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopCts.Cancel(); };
try { await Task.Delay(Timeout.InfiniteTimeSpan, stopCts.Token); } catch (OperationCanceledException) { }

Console.WriteLine("Stopping soak…");
wocCts.Cancel();
reconcileCts.Cancel();
try { await wocLoop; } catch { }
try { await reconcileLoop; } catch { }
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

static string ReverseHex(string hex)
{
    if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0) return hex;
    var bytes = Convert.FromHexString(hex);
    Array.Reverse(bytes);
    return Convert.ToHexString(bytes).ToLowerInvariant();
}
