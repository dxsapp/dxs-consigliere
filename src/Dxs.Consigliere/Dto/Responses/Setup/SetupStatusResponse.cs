namespace Dxs.Consigliere.Dto.Responses.Setup;

public sealed class SetupStatusResponse
{
    public bool SetupRequired { get; set; }
    public bool SetupCompleted { get; set; }
    public bool AdminEnabled { get; set; }
    public string AdminUsername { get; set; }
}

public sealed class SetupOptionsResponse
{
    public SetupStatusResponse Status { get; set; } = new();
    public SetupDefaultsResponse Defaults { get; set; } = new();
    public SetupAllowedOptionsResponse Allowed { get; set; } = new();
    public SetupProviderFormDefaultsResponse ProviderConfig { get; set; } = new();
}

public sealed class SetupDefaultsResponse
{
    public string RawTxPrimaryProvider { get; set; }
    public string RestFallbackProvider { get; set; }
    public string RealtimePrimaryProvider { get; set; }
    public string BitailsTransport { get; set; }
}

public sealed class SetupAllowedOptionsResponse
{
    public string[] RawTxPrimaryProviders { get; set; } = [];
    public string[] RestFallbackProviders { get; set; } = [];
    public string[] RealtimePrimaryProviders { get; set; } = [];
    public string[] BitailsTransports { get; set; } = [];
}

public sealed class SetupProviderFormDefaultsResponse
{
    public SetupBitailsProviderDefaultsResponse Bitails { get; set; } = new();
    public SetupRestProviderDefaultsResponse Whatsonchain { get; set; } = new();
    public SetupJungleBusProviderDefaultsResponse Junglebus { get; set; } = new();
    public SetupNodeProviderDefaultsResponse Node { get; set; } = new();
}

public sealed class SetupBitailsProviderDefaultsResponse
{
    public string ApiKey { get; set; }
    public string BaseUrl { get; set; }
    public string WebsocketBaseUrl { get; set; }
    public string ZmqTxUrl { get; set; }
    public string ZmqBlockUrl { get; set; }
}

public sealed class SetupRestProviderDefaultsResponse
{
    public string ApiKey { get; set; }
    public string BaseUrl { get; set; }
}

public sealed class SetupJungleBusProviderDefaultsResponse
{
    public string ApiKey { get; set; }
    public string BaseUrl { get; set; }
    public string MempoolSubscriptionId { get; set; }
    public string BlockSubscriptionId { get; set; }
}

public sealed class SetupNodeProviderDefaultsResponse
{
    public string ZmqTxUrl { get; set; }
    public string ZmqBlockUrl { get; set; }
}
