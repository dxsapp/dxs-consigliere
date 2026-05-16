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

    public const int DefaultTxMaxSizeBytes = 2 * 1024 * 1024;
}

public sealed class TxPolicyConfig
{
    public int MaxRawSizeBytes { get; set; } = BsvP2pConfig.DefaultTxMaxSizeBytes;

    /// <summary>Minimum fee in satoshis per kilobyte (0 = no floor).</summary>
    public long MinFeePerKbSat { get; set; } = 0;
}
