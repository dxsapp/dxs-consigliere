using Microsoft.Extensions.Configuration;

namespace Dxs.Consigliere.Configs;

public class ConsigliereSourcesConfig
{
    public SourceProvidersConfig Providers { get; set; } = new();
    public SourceRoutingConfig Routing { get; set; } = new();
    public SourceCapabilitiesConfig Capabilities { get; set; } = new();
}

public class SourceProvidersConfig
{
    public NodeSourceConfig Node { get; set; } = new();
    public JungleBusSourceConfig JungleBus { get; set; } = new();
    public BitailsSourceConfig Bitails { get; set; } = new();
    public WhatsOnChainSourceConfig Whatsonchain { get; set; } = new();
}

public class SourceRoutingConfig
{
    public string PreferredMode { get; set; }
    public string PrimarySource { get; set; }
    public string[] FallbackSources { get; set; } = [];
    public string VerificationSource { get; set; }
}

public class SourceCapabilitiesConfig
{
    public BroadcastCapabilityOverrideConfig Broadcast { get; set; } = new();

    [ConfigurationKeyName("realtime_ingest")]
    public RoutedCapabilityOverrideConfig RealtimeIngest { get; set; } = new();

    [ConfigurationKeyName("block_backfill")]
    public RoutedCapabilityOverrideConfig BlockBackfill { get; set; } = new();

    [ConfigurationKeyName("raw_tx_fetch")]
    public RoutedCapabilityOverrideConfig RawTxFetch { get; set; } = new();

    [ConfigurationKeyName("validation_fetch")]
    public RoutedCapabilityOverrideConfig ValidationFetch { get; set; } = new();

    [ConfigurationKeyName("historical_address_scan")]
    public RoutedCapabilityOverrideConfig HistoricalAddressScan { get; set; } = new();

    [ConfigurationKeyName("historical_token_scan")]
    public RoutedCapabilityOverrideConfig HistoricalTokenScan { get; set; } = new();
}

public class BroadcastCapabilityOverrideConfig : RoutedCapabilityOverrideConfig
{
    public string Mode { get; set; }
    public string[] Sources { get; set; } = [];
}

public class RoutedCapabilityOverrideConfig
{
    public string Source { get; set; }
    public string[] FallbackSources { get; set; } = [];
}

public class SourceRateLimitConfig
{
    public int? RequestsPerMinute { get; set; }

    [ConfigurationKeyName("perCapability")]
    public Dictionary<string, SourceRateLimitConfig> PerCapability { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class SourceProviderConfig
{
    public bool Enabled { get; set; }
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan? StreamTimeout { get; set; }
    public TimeSpan? IdleTimeout { get; set; }
    public string[] EnabledCapabilities { get; set; } = [];
    public SourceRateLimitConfig RateLimits { get; set; }
}

public class NodeSourceConfig : SourceProviderConfig
{
    public NodeSourceConnectionConfig Connection { get; set; } = new();
}

public class JungleBusSourceConfig : SourceProviderConfig
{
    public JungleBusSourceConnectionConfig Connection { get; set; } = new();
}

public class BitailsSourceConfig : SourceProviderConfig
{
    public HttpApiSourceConnectionConfig Connection { get; set; } = new();
}

public class WhatsOnChainSourceConfig : SourceProviderConfig
{
    public HttpApiSourceConnectionConfig Connection { get; set; } = new();
}

public class NodeSourceConnectionConfig
{
    public string RpcUrl { get; set; }
    public string RpcUser { get; set; }
    public string RpcPassword { get; set; }
    public string ZmqTxUrl { get; set; }
    public string ZmqBlockUrl { get; set; }
}

public class JungleBusSourceConnectionConfig
{
    public string BaseUrl { get; set; }
    public string ApiKey { get; set; }
}

public class HttpApiSourceConnectionConfig
{
    public string BaseUrl { get; set; }
    public string ApiKey { get; set; }
}
