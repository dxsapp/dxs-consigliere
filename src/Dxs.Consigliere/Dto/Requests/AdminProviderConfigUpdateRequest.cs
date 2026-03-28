namespace Dxs.Consigliere.Dto.Requests;

public sealed class AdminProviderConfigUpdateRequest
{
    public string RealtimePrimaryProvider { get; set; }
    public string RestPrimaryProvider { get; set; }
    public string BitailsTransport { get; set; }
    public AdminBitailsProviderConfigUpdateRequest Bitails { get; set; } = new();
    public AdminRestProviderConfigUpdateRequest Whatsonchain { get; set; } = new();
    public AdminJungleBusProviderConfigUpdateRequest Junglebus { get; set; } = new();
}

public sealed class AdminBitailsProviderConfigUpdateRequest
{
    public string ApiKey { get; set; }
    public string BaseUrl { get; set; }
    public string WebsocketBaseUrl { get; set; }
    public string ZmqTxUrl { get; set; }
    public string ZmqBlockUrl { get; set; }
}

public sealed class AdminRestProviderConfigUpdateRequest
{
    public string ApiKey { get; set; }
    public string BaseUrl { get; set; }
}

public sealed class AdminJungleBusProviderConfigUpdateRequest
{
    public string BaseUrl { get; set; }
    public string MempoolSubscriptionId { get; set; }
    public string BlockSubscriptionId { get; set; }
}
