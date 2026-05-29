#nullable enable
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
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class SetupProviderSelectionRequest
{
    public string RawTxPrimaryProvider { get; set; } = string.Empty;
    public string RestFallbackProvider { get; set; } = string.Empty;
    public string RealtimePrimaryProvider { get; set; } = string.Empty;
    public string BitailsTransport { get; set; } = string.Empty;
    public AdminBitailsProviderConfigUpdateRequest Bitails { get; set; } = new();
    public AdminRestProviderConfigUpdateRequest Whatsonchain { get; set; } = new();
    public AdminJungleBusProviderConfigUpdateRequest Junglebus { get; set; } = new();
    public SetupNodeRealtimeConfigRequest Node { get; set; } = new();
}

public sealed class SetupNodeRealtimeConfigRequest
{
    public string ZmqTxUrl { get; set; } = string.Empty;
    public string ZmqBlockUrl { get; set; } = string.Empty;
}

public sealed class SetupJungleBusBlockSyncRequest
{
    public string BaseUrl { get; set; } = string.Empty;
    public string BlockSubscriptionId { get; set; } = string.Empty;
}
