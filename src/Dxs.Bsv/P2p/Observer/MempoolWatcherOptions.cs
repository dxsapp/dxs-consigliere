#nullable enable
namespace Dxs.Bsv.P2p.Observer;

/// <summary>
/// Wave 2 S4 config for the BSV mempool watcher.
/// </summary>
public sealed record MempoolWatcherOptions
{
    /// <summary>Soft cap on getdata requests per second across all peers.
    /// Default 200 — BSV mempool tx-arrival rate during peak hours is
    /// roughly 1k/s including dedupes; 200 unique getdatas / s is
    /// comfortably above the de-duplicated rate.</summary>
    public int MaxGetDataPerSec { get; init; } = 200;

    /// <summary>How long a txid stays in the dedupe cache before it can
    /// be re-fetched. Default 10 minutes — longer than typical block
    /// time so we don't refetch the same mempool tx across peers.</summary>
    public int DedupeTtlSeconds { get; init; } = 600;

    /// <summary>Maximum tx payload (bytes) we'll accept on a getdata reply.
    /// Default 32 MiB matches BSV mainnet typical mempool acceptance
    /// ceiling; raised from the legacy 2 MiB session default per audit
    /// W2 M4. Mirrors <c>BsvP2pConfig.MempoolMaxFetchedTxBytes</c>.</summary>
    public int MaxFetchedTxBytes { get; init; } = 32 * 1024 * 1024;

    /// <summary>How long to wait for the tx frame after issuing getdata
    /// before declaring it a timeout and unsubscribing the one-shot
    /// handler. Default 30 seconds.</summary>
    public int GetDataTimeoutMs { get; init; } = 30_000;

    /// <summary>Hard ceiling on the number of txids tracked in the dedupe
    /// cache. Prevents the cache from growing unbounded on a busy
    /// mempool. Older entries get evicted to honour the cap. Default
    /// 100_000 — roughly an hour of mainnet mempool inflow.</summary>
    public int DedupeMaxEntries { get; init; } = 100_000;
}
