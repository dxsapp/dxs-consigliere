#nullable enable
namespace Dxs.Bsv.P2p.Chain;

/// <summary>
/// Tunables for the in-memory <see cref="HeadersChain"/> + the hosted
/// service that drives it (S3 in
/// docs/stream-tasks/bsv-headers-chain-wave/).
/// </summary>
public sealed record HeadersChainOptions
{
    /// <summary>
    /// Maximum number of recent headers retained in memory and in Raven.
    /// Headers older than (tip - RetainedHeaderCount) are pruned by the
    /// background pass. Default 200 is intentionally shallow — the chain
    /// is a working window, not a historical archive.
    /// </summary>
    public int RetainedHeaderCount { get; init; } = 200;

    /// <summary>
    /// Cadence at which the headers service sends a baseline
    /// <c>getheaders</c> to a ready peer to catch any missed tip notifications.
    /// </summary>
    public int GetHeadersIntervalMs { get; init; } = 30_000;

    /// <summary>
    /// When true, on cold start the service seeds the chain from
    /// Bitails REST. When false, the service waits for the first P2P
    /// <c>inv(MSG_BLOCK)</c> or <c>headers</c> to populate the chain.
    /// </summary>
    public bool SeedFromBitails { get; init; } = true;

    /// <summary>Bootstrap deadline before falling back to pure P2P.</summary>
    public int BootstrapTimeoutMs { get; init; } = 10_000;
}
