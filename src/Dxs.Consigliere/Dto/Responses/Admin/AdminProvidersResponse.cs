namespace Dxs.Consigliere.Dto.Responses.Admin;

public sealed class AdminProvidersResponse
{
    public AdminProviderRecommendationsResponse Recommendations { get; set; } = new();
    public AdminProviderConfigResponse Config { get; set; } = new();
    public AdminProviderCatalogItemResponse[] Providers { get; set; } = [];
}

public sealed class AdminProviderRecommendationsResponse
{
    public string RealtimePrimaryProvider { get; set; }
    public string RestPrimaryProvider { get; set; }
}

public sealed class AdminProviderConfigResponse
{
    public AdminProviderConfigValuesResponse Static { get; set; }
    public AdminProviderConfigValuesResponse Override { get; set; }
    public AdminProviderConfigValuesResponse Effective { get; set; }
    public bool OverrideActive { get; set; }
    public bool RestartRequired { get; set; }
    public string[] AllowedRealtimePrimaryProviders { get; set; } = [];
    public string[] AllowedRestPrimaryProviders { get; set; } = [];
    public string[] AllowedBitailsTransports { get; set; } = [];
    public long? UpdatedAt { get; set; }
    public string UpdatedBy { get; set; }
}

public sealed class AdminProviderConfigValuesResponse
{
    public string RealtimePrimaryProvider { get; set; }
    public string RestPrimaryProvider { get; set; }
    public string BitailsTransport { get; set; }
    public AdminBitailsProviderConfigResponse Bitails { get; set; } = new();
    public AdminRestProviderConfigResponse Whatsonchain { get; set; } = new();
    public AdminJungleBusProviderConfigResponse Junglebus { get; set; } = new();
}

public sealed class AdminBitailsProviderConfigResponse
{
    public string ApiKey { get; set; }
    public string BaseUrl { get; set; }
    public string WebsocketBaseUrl { get; set; }
    public string ZmqTxUrl { get; set; }
    public string ZmqBlockUrl { get; set; }
}

public sealed class AdminRestProviderConfigResponse
{
    public string ApiKey { get; set; }
    public string BaseUrl { get; set; }
}

public sealed class AdminJungleBusProviderConfigResponse
{
    public string BaseUrl { get; set; }
    public string MempoolSubscriptionId { get; set; }
    public string BlockSubscriptionId { get; set; }
}

public sealed class AdminProviderCatalogItemResponse
{
    public string ProviderId { get; set; }
    public string DisplayName { get; set; }
    public string[] Roles { get; set; } = [];
    public string[] SupportedCapabilities { get; set; } = [];
    public string[] RecommendedFor { get; set; } = [];
    public string[] ActiveFor { get; set; } = [];
    public string Status { get; set; }
    public string Description { get; set; }
    public string[] MissingRequirements { get; set; } = [];
    public AdminProviderLinkResponse[] HelpLinks { get; set; } = [];
}

public sealed class AdminProviderLinkResponse
{
    public string Label { get; set; }
    public string Url { get; set; }
}
