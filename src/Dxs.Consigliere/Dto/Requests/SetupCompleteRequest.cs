namespace Dxs.Consigliere.Dto.Requests;

public sealed class SetupCompleteRequest
{
    public SetupAdminAccessRequest Admin { get; set; } = new();
    public SetupProviderSelectionRequest Providers { get; set; } = new();
    public SetupJungleBusBlockSyncRequest BlockSync { get; set; } = new();
}

public sealed class SetupAdminAccessRequest
{
    public bool Enabled { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}

public sealed class SetupProviderSelectionRequest
{
    public string RawTxPrimaryProvider { get; set; }
    public string RestFallbackProvider { get; set; }
    public string RealtimePrimaryProvider { get; set; }
    public string BitailsTransport { get; set; }
    public AdminBitailsProviderConfigUpdateRequest Bitails { get; set; } = new();
    public AdminRestProviderConfigUpdateRequest Whatsonchain { get; set; } = new();
    public AdminJungleBusProviderConfigUpdateRequest Junglebus { get; set; } = new();
    public SetupNodeRealtimeConfigRequest Node { get; set; } = new();
}

public sealed class SetupNodeRealtimeConfigRequest
{
    public string ZmqTxUrl { get; set; }
    public string ZmqBlockUrl { get; set; }
}

public sealed class SetupJungleBusBlockSyncRequest
{
    public string BaseUrl { get; set; }
    public string BlockSubscriptionId { get; set; }
}
