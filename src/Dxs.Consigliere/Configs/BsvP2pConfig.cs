using System.Collections.Generic;

namespace Dxs.Consigliere.Configs;

/// <summary>
/// Operator-facing configuration for the BSV thin-node P2p subsystem
/// (Gate 2 of consigliere-thin-node-design.md).
/// Bound from <c>Consigliere:Broadcast:P2p</c> in appsettings.
/// </summary>
public sealed class BsvP2pConfig
{
    /// <summary>Master switch. Default <c>false</c> — Gate 2 ships disabled-by-default.</summary>
    public bool Enabled { get; set; } = false;

    public string Network { get; set; } = "mainnet";

    public int PoolSize { get; set; } = 8;

    public int BootstrapMaxConcurrency { get; set; } = 4;

    public int BootstrapJitterMs { get; set; } = 200;

    public int ConnectTimeoutMs { get; set; } = 5000;

    public int HandshakeTimeoutMs { get; set; } = 30000;

    public int NegativeCooldownMinutes { get; set; } = 15;

    public int MaintenanceIntervalSeconds { get; set; } = 15;

    public int DnsRefreshIntervalHours { get; set; } = 6;

    public List<string> InitialPeers { get; set; } = new();

    public string UserAgent { get; set; } = "/ConsigliereThinNode:0.1.0/";

    public ulong Services { get; set; } = 0x25;

    /// <summary>Send <c>protoconf</c> after our <c>verack</c> (BSV compatibility).</summary>
    public bool SendProtoconfAfterVerack { get; set; } = true;

    /// <summary>Gate 3 transaction policy. Null = defaults apply.</summary>
    public TxPolicyConfig TxPolicy { get; set; } = new();

    /// <summary>Wave 6 S2 — alert poller thresholds + retention.</summary>
    public AlertConfig Alert { get; set; } = new();

    /// <summary>
    /// Wave 6 S4 — inbound P2P listener decision. Config-only stub:
    /// the W6 implementation does NOT ship listener code. Setting
    /// <c>Enabled=true</c> logs a warning + leaves
    /// <see cref="Configs.AlertConfig"/> behaviour unchanged. The
    /// flag exists so a future wave can land inbound without a
    /// contract amendment.
    /// </summary>
    public InboundConfig Inbound { get; set; } = new();

    /// <summary>
    /// Wave 2 S4 — maximum P2P tx payload accepted by mempool
    /// observation (audit W2 M4). Default 32 MiB matches BSV
    /// mainnet typical mempool acceptance ceiling; raised from the
    /// legacy 2 MiB session default. Propagates into
    /// <c>PeerSessionConfig.InitialMaxRecvPayloadLength</c> at
    /// session construction time (see
    /// <c>BsvP2pHostedService.BuildSessionConfig</c>). Mirrored by
    /// <c>MempoolWatcherOptions.MaxFetchedTxBytes</c>.
    /// </summary>
    public int MempoolMaxFetchedTxBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>
    /// thin-node-primary-source S2 — bound for the on-demand
    /// <c>getdata(MSG_TX)</c> rawTx fetch issued by
    /// <c>IP2pRawTransactionClient</c> when <c>p2p</c> is the resolved
    /// rawTx primary. On expiry the client returns <c>null</c> so
    /// <c>RawTransactionFetchService</c> falls through to the external
    /// providers (whatsonchain / junglebus / bitails). Default 3000 ms —
    /// long enough for a mempool-resident tx to arrive from a ready peer,
    /// short enough not to stall a confirmed-tx fetch's fallback.
    /// </summary>
    public int RawTxFetchTimeoutMs { get; set; } = 3000;

    public const int DefaultTxMaxSizeBytes = 2 * 1024 * 1024;
}

public sealed class TxPolicyConfig
{
    public int MaxRawSizeBytes { get; set; } = BsvP2pConfig.DefaultTxMaxSizeBytes;

    /// <summary>Minimum fee in satoshis per kilobyte (0 = no floor).</summary>
    public long MinFeePerKbSat { get; set; } = 0;
}

/// <summary>
/// Wave 6 S2 — operator-tunable thresholds + cadence for the
/// P2P alert poller. Defaults match master.md §"Critical alert
/// poller" (A1-followup M3).
/// </summary>
/// <summary>
/// Wave 6 S4 — inbound P2P listener config stub. <see cref="Enabled"/>
/// is a no-op flag in the W6 release: the hosted service logs a
/// one-line warning at startup and never spins a listener thread.
/// The shape is frozen for a future inbound-listener wave (master.md
/// handoff table).
/// </summary>
public sealed class InboundConfig
{
    /// <summary>Default false — see master.md §"Product Decision".</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Reserved TCP port for the future listener.
    /// Default 8333 matches mainnet conventions; ignored while
    /// <see cref="Enabled"/> is false.</summary>
    public int ListenPort { get; set; } = 8333;
}

public sealed class AlertConfig
{
    /// <summary>Master switch. Default false — operator enables in production.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Poll cadence. Default 60 s.</summary>
    public int AlertPollIntervalMs { get; set; } = 60_000;

    /// <summary>Retain at most this many alert documents. Default 720 ≈ 12 h at 60 s cadence.</summary>
    public int AlertRetentionEvents { get; set; } = 720;

    /// <summary>Pool size below this fires PoolSizeBelowThreshold. Default 5.</summary>
    public int MinPoolSize { get; set; } = 5;

    /// <summary>Relay-back rate below this fires RelayBackRateBelowThreshold (only when
    /// sum(ΔGetDataRequested) > 0 — see A1 pass-2 H1 fix). Default 0.30.</summary>
    public double MinRelayBackRate { get; set; } = 0.30;

    /// <summary>If <c>BsvP2pHealth.LastDegradedReorgAt</c> is within this window of now,
    /// fires ReorgDepthExceeded. Default 5 minutes (300_000 ms).</summary>
    public int ReorgDepthWindowMs { get; set; } = 5 * 60 * 1000;

    /// <summary>If a source's FirstSeen delta is zero across snapshots spanning this
    /// window, fires SourceFirstDropout. Default 1 hour (3_600_000 ms).</summary>
    public int SourceFirstDropoutWindowMs { get; set; } = 60 * 60 * 1000;
}
