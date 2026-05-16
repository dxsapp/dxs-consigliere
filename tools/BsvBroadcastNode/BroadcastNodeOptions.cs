namespace BsvBroadcastNode;

public sealed class BroadcastNodeOptions
{
    public const string Section = "BroadcastNode";

    public bool Enabled { get; set; } = true;

    /// <summary>Target size for the outbound peer pool.</summary>
    public int PoolSize { get; set; } = 8;

    /// <summary>
    /// User-agent advertised to BSV peers.
    /// Operators running their own seed peers may require /Bitcoin SV:X.Y.Z/.
    /// </summary>
    public string UserAgent { get; set; } = "/Bitcoin SV:1.2.1/";

    public ulong Services { get; set; } = 0x25;

    /// <summary>Comma-separated host:port pairs used in addition to DNS seeds.</summary>
    public string ExtraPeers { get; set; } = "";

    /// <summary>Max raw transaction size in bytes (Phase 1 = 2 MB).</summary>
    public int MaxTxSizeBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>How many recent log lines to keep in-memory for GET /log.</summary>
    public int LogRingSize { get; set; } = 500;

    /// <summary>BSV P2P port to listen for inbound peer connections. 0 = disabled.</summary>
    public int ListenPort { get; set; } = 8333;

    public List<string> ParseExtraPeers() =>
        ExtraPeers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
