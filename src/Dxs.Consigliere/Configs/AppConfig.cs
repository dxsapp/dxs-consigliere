namespace Dxs.Consigliere.Configs;

public class AppConfig
{
    public bool ScanMempoolOnStart { get; init; }
    public int BlockCountToScanOnStart { get; set; }

    public BackgroundTasksConfig BackgroundTasks { get; set; } = new();

    public JungleBusConfig JungleBus { get; set; }
}
