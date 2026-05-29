#nullable enable
namespace Dxs.Consigliere.Dto.Requests;

public sealed class AdminProviderConfigUpdateRequest
{
    public string RealtimePrimaryProvider { get; set; } = string.Empty;
    public string RawTxPrimaryProvider { get; set; } = string.Empty;
    public string RestPrimaryProvider { get; set; } = string.Empty;
    public string BitailsTransport { get; set; } = string.Empty;
    public AdminBitailsProviderConfigUpdateRequest Bitails { get; set; } = new();
    public AdminRestProviderConfigUpdateRequest Whatsonchain { get; set; } = new();
    public AdminJungleBusProviderConfigUpdateRequest Junglebus { get; set; } = new();
}

public sealed class AdminBitailsProviderConfigUpdateRequest
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string WebsocketBaseUrl { get; set; } = string.Empty;
    public string ZmqTxUrl { get; set; } = string.Empty;
    public string ZmqBlockUrl { get; set; } = string.Empty;
}

public sealed class AdminRestProviderConfigUpdateRequest
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}

public sealed class AdminJungleBusProviderConfigUpdateRequest
{
    public string BaseUrl { get; set; } = string.Empty;
    public string MempoolSubscriptionId { get; set; } = string.Empty;
    public string BlockSubscriptionId { get; set; } = string.Empty;
}
