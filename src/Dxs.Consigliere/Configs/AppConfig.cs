namespace Dxs.Consigliere.Configs;

public class AppConfig
{
    public bool ScanMempoolOnStart { get; init; }
    public int BlockCountToScanOnStart { get; set; }

    public BackgroundTasksConfig BackgroundTasks { get; set; } = new();
    public VNextRuntimeConfig VNextRuntime { get; set; } = new();
    public ValidationConfig Validation { get; set; } = new();

    public JungleBusConfig JungleBus { get; set; }
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
