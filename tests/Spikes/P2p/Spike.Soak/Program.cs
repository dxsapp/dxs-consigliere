// Gate 2 soak — standalone P2P peer pool test.
// No RavenDB, no Consigliere, no external dependencies. Pure network.
//
// Usage:  dotnet run --project Spike.Soak
// Docker: see Dockerfile in this folder

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Pool;
using Dxs.Bsv.P2p.Session;

// ── Config from env or defaults ───────────────────────────────────────────
var targetPool   = GetInt("POOL_SIZE",       8);
var soakMinutes  = GetInt("SOAK_MINUTES",    1440); // 24h default; set 60 for a quick run
var statsIntervalSec = GetInt("STATS_INTERVAL_SEC", 60);
var userAgent    = Env("USER_AGENT",  "/ConsigliereThinNode:0.1.0/");
var extraPeers   = Env("EXTRA_PEERS", ""); // comma-separated host:port overrides

// ── Bootstrap ─────────────────────────────────────────────────────────────
var network    = P2pNetwork.Mainnet;
var store      = new InMemoryPeerStore();
var discovery  = new PeerDiscovery(network, store);
var sendProtoconf = !string.Equals(Env("SEND_PROTOCONF", "true"), "false", StringComparison.OrdinalIgnoreCase);

var config = new PeerManagerConfig
{
    TargetPoolSize           = targetPool,
    BootstrapMaxConcurrency  = 4,
    BootstrapJitter          = TimeSpan.FromMilliseconds(200),
    NegativeCooldown         = TimeSpan.FromMinutes(15),
    MaintenanceInterval      = TimeSpan.FromSeconds(15),
    DnsRefreshInterval       = TimeSpan.FromHours(6),
    VersionFactory           = BuildVersion,
    SessionConfig            = new PeerSessionConfig { SendProtoconfAfterVerack = sendProtoconf },
};

// Add operator-supplied peers first (if any)
foreach (var raw in extraPeers.Split(',', StringSplitOptions.RemoveEmptyEntries))
{
    if (TryParseEndpoint(raw.Trim(), 8333, out var ep))
        await discovery.AddSeedAsync(ep, CancellationToken.None);
}

await using var manager = new PeerManager(network, discovery, store, config);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Print($"BSV thin-node soak — target pool {targetPool}, soak {soakMinutes} min, UA: {userAgent}");
Print($"DNS seeds: {string.Join(", ", network.DnsSeeds)}");
Print("Press Ctrl+C to stop early. Stats every 60s.");
Print(new string('-', 90));

await manager.StartAsync(CancellationToken.None);

var soakDeadline = DateTime.UtcNow.AddMinutes(soakMinutes);
var statsTimer   = DateTime.UtcNow;
var hourBuckets  = new Dictionary<int, BucketStats>();   // hour-of-run → stats

while (!cts.IsCancellationRequested && DateTime.UtcNow < soakDeadline)
{
    var elapsed = DateTime.UtcNow - (soakDeadline - TimeSpan.FromMinutes(soakMinutes));
    var hour    = (int)elapsed.TotalHours;

    // Per-minute stats tick
    if ((DateTime.UtcNow - statsTimer).TotalSeconds >= statsIntervalSec)
    {
        statsTimer = DateTime.UtcNow;
        var peers  = manager.ActiveSessions;
        var all    = await store.ListAllAsync(CancellationToken.None);
        var succ   = all.Count(r => r.SuccessCount > 0);
        var fail   = all.Count(r => r.FailCount > 0 && r.SuccessCount == 0);
        var total  = all.Count;
        var rate   = total > 0 ? (succ * 100.0 / total) : 0;

        var sb = new StringBuilder();
        sb.Append($"[{elapsed:hh\\:mm}] pool {manager.PoolSize}/{targetPool} ");
        sb.Append($"| subnets {manager.Subnet24Diversity} ");
        sb.Append($"| seen {total} peers (ok={succ} fail={fail} rate={rate:F0}%) ");

        var uas = peers.Values
            .Where(s => s.PeerVersion?.UserAgent is not null)
            .Select(s => s.PeerVersion!.UserAgent!)
            .GroupBy(u => ExtractVersion(u))
            .Select(g => $"{g.Key}×{g.Count()}")
            .ToList();
        if (uas.Any()) sb.Append($"| {string.Join(" ", uas)}");

        Print(sb.ToString());

        // Accumulate hourly bucket
        if (!hourBuckets.TryGetValue(hour, out var bucket))
            hourBuckets[hour] = bucket = new BucketStats();
        bucket.Samples++;
        bucket.PoolSizeSum += manager.PoolSize;
        bucket.SuccessTotal = succ;
        bucket.TotalSeen    = total;
    }

    await Task.Delay(2000, cts.Token).ContinueWith(_ => { });
}

// ── Final report ───────────────────────────────────────────────────────────
Print(new string('=', 90));
Print("SOAK COMPLETE — Final Report");
Print(new string('=', 90));

var finalAll     = await store.ListAllAsync(CancellationToken.None);
var finalSucc    = finalAll.Count(r => r.SuccessCount > 0);
var finalFail    = finalAll.Count(r => r.FailCount > 0 && r.SuccessCount == 0);
var finalRate    = finalAll.Count > 0 ? (finalSucc * 100.0 / finalAll.Count) : 0;
var finalSubnets = finalAll.Where(r => r.SuccessCount > 0).Select(r => r.Subnet24).Distinct().Count();

Print($"Total peers seen:          {finalAll.Count}");
Print($"Successful handshakes:     {finalSucc} ({finalRate:F1}%)");
Print($"Failed (never connected):  {finalFail}");
Print($"Distinct /24 subnets:      {finalSubnets}");
Print(new string('-', 90));

// Threshold check against §3.2 of design doc
var coldStartRate = hourBuckets.ContainsKey(0) && hourBuckets[0].TotalSeen > 0
    ? hourBuckets[0].SuccessTotal * 100.0 / hourBuckets[0].TotalSeen : 0;
var avgPoolSize = hourBuckets.Values.Count > 0
    ? hourBuckets.Values.Average(b => b.Samples > 0 ? (double)b.PoolSizeSum / b.Samples : 0) : 0;

Print("§3.2 Threshold check:");
PrintThreshold("Pool size p50 (avg)", $"{avgPoolSize:F1}", avgPoolSize >= targetPool ? "✅ PASS" : avgPoolSize >= 4 ? "⚠ INVESTIGATE" : "❌ FAIL");
PrintThreshold("Cold-start acceptance (hour 0)", $"{coldStartRate:F0}%", coldStartRate >= 30 ? "✅ PASS" : coldStartRate >= 10 ? "⚠ INVESTIGATE" : "❌ FAIL");
PrintThreshold("Subnet diversity", $"{finalSubnets}", finalSubnets >= 3 ? "✅ PASS" : finalSubnets >= 2 ? "⚠ INVESTIGATE" : "❌ FAIL");

Print(new string('-', 90));
Print("Top 10 most stable peers:");
foreach (var r in finalAll.OrderByDescending(r => r.SuccessCount).Take(10))
{
    Print($"  {r.Key,-25} ua={r.UserAgent ?? "-",-22} ok={r.SuccessCount} fail={r.FailCount}");
}

// ── Helpers ────────────────────────────────────────────────────────────────
VersionMessage BuildVersion() => new(
    ProtocolVersion: VersionMessage.CurrentProtocolVersion,
    Services: 0x25,
    TimestampUnixSeconds: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
    AddrRecv: P2pAddress.Anonymous(0x25),
    AddrFrom: P2pAddress.Anonymous(0x25),
    Nonce: (ulong)Random.Shared.NextInt64(),
    UserAgent: userAgent,
    StartHeight: 0,
    Relay: true,
    AssociationId: null);

static int GetInt(string key, int def) =>
    int.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : def;

static string Env(string key, string def) =>
    Environment.GetEnvironmentVariable(key) ?? def;

static void Print(string msg) =>
    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {msg}");

static void PrintThreshold(string name, string value, string verdict) =>
    Console.WriteLine($"  {name,-35} {value,-10} {verdict}");

static string ExtractVersion(string ua)
{
    var start = ua.IndexOf(':');
    if (start < 0) return ua;
    var end = ua.IndexOf('/', start);
    if (end < 0) return ua;
    return ua[(start + 1)..end];
}

static bool TryParseEndpoint(string raw, int defaultPort, out IPEndPoint endpoint)
{
    endpoint = default!;
    var colon = raw.LastIndexOf(':');
    string host;
    int port = defaultPort;
    if (colon > 0 && !raw.Contains("::"))
    {
        host = raw[..colon];
        if (!int.TryParse(raw[(colon + 1)..], out port)) return false;
    }
    else
    {
        host = raw;
    }
    if (!IPAddress.TryParse(host, out var addr)) return false;
    endpoint = new IPEndPoint(addr, port);
    return true;
}

sealed class BucketStats
{
    public int Samples;
    public int PoolSizeSum;
    public int SuccessTotal;
    public int TotalSeen;
}
