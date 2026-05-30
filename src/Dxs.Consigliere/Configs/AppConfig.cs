namespace Dxs.Consigliere.Configs;

public class AppConfig
{
    public bool ScanMempoolOnStart { get; init; }
    public int BlockCountToScanOnStart { get; set; }

    public BackgroundTasksConfig BackgroundTasks { get; set; } = new();
    public VNextRuntimeConfig VNextRuntime { get; set; } = new();
    public ValidationConfig Validation { get; set; } = new();
    public RealtimeConfig Realtime { get; set; } = new();

    public JungleBusConfig JungleBus { get; set; }
}

public class RealtimeConfig
{
    /// <summary>
    /// When false (default), the external realtime runners (Bitails /
    /// JungleBus) run ONLY when one of them is the resolved realtime
    /// primary. With the thin node (<c>p2p</c>) as primary this means NO
    /// external realtime runner runs — the in-house P2P observer is the sole
    /// realtime source, and the node makes no third-party provider calls for
    /// realtime ingest (e.g. Bitails' per-tx raw-tx fetch → whatsonchain).
    /// Set true to run every enabled external realtime runner concurrently
    /// for redundancy + "who saw it first" metrics (the thin-node-primary S3
    /// behaviour) — at the cost of provider traffic.
    /// </summary>
    public bool ExternalRedundancyEnabled { get; set; } = false;
}

public class ValidationConfig
{
    /// <summary>
    /// When false (default), transactions are validated using ONLY
    /// locally-available data; the node does NOT walk transaction ancestry
    /// by fetching missing parent transactions from external providers on
    /// the fly. This stops the reverse-lineage validation-dependency repair
    /// (and its self-feeding loop that fetched ancestors of out-of-scope
    /// STAS tx via whatsonchain/junglebus). Set true only if you want the
    /// node to actively backfill missing ancestry from providers.
    /// </summary>
    public bool ReverseLineageRepairEnabled { get; set; } = false;
}
