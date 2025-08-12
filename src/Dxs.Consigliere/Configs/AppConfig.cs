namespace Dxs.Consigliere.Configs;

public class AppConfig
{
    public bool ScanMempoolOnStart { get; init; }
    public int BlockCountToScanOnStart { get; set; }

    /// <summary>
    /// Addresses to which users without bridge account can send via pay my fee
    /// </summary>
    public string[] AllowedAddresses { get; set; }

    public BackgroundTasksConfig BackgroundTasks { get; set; } = new();

    public BsvWallets BsvWallets { get; set; }
    public JungleBusConfig JungleBus { get; set; }
    public BitailsConfig Bitails { get; set; }
    public TaalConfig Taal { get; set; }
}