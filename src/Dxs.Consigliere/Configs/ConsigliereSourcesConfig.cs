using Microsoft.Extensions.Configuration;
using Dxs.Infrastructure.Common;

namespace Dxs.Consigliere.Configs;

public class ConsigliereSourcesConfig
{
    public SourceProvidersConfig Providers { get; set; } = SourceProvidersConfig.CreateDefaults();
    public SourceRoutingConfig Routing { get; set; } = SourceRoutingConfig.CreateDefaults();
    public SourceCapabilitiesConfig Capabilities { get; set; } = SourceCapabilitiesConfig.CreateDefaults();
}

public class SourceProvidersConfig
{
    public NodeSourceConfig Node { get; set; } = new();
    public JungleBusSourceConfig JungleBus { get; set; } = new();
    public BitailsSourceConfig Bitails { get; set; } = new();
    public WhatsOnChainSourceConfig Whatsonchain { get; set; } = new();

    public static SourceProvidersConfig CreateDefaults()
        => new()
        {
            Node = new NodeSourceConfig
            {
                Enabled = true,
                EnabledCapabilities =
                [
                    ExternalChainCapability.Broadcast,
                    ExternalChainCapability.RealtimeIngest,
                    ExternalChainCapability.BlockBackfill,
                    ExternalChainCapability.RawTxFetch,
                    ExternalChainCapability.ValidationFetch
                ]
            },
            JungleBus = new JungleBusSourceConfig
            {
                Enabled = true,
                EnabledCapabilities =
                [
                    ExternalChainCapability.RawTxFetch,
                    ExternalChainCapability.RealtimeIngest,
                    ExternalChainCapability.BlockBackfill
                ],
                Connection = new JungleBusSourceConnectionConfig
                {
                    BaseUrl = "https://junglebus.gorillapool.io"
                }
            },
            Bitails = new BitailsSourceConfig
            {
                Enabled = true,
                EnabledCapabilities =
                [
                    ExternalChainCapability.Broadcast,
                    ExternalChainCapability.RealtimeIngest,
                    ExternalChainCapability.RawTxFetch,
                    ExternalChainCapability.ValidationFetch,
                    ExternalChainCapability.HistoricalAddressScan,
                    ExternalChainCapability.HistoricalTokenScan
                ],
                Connection = new BitailsSourceConnectionConfig
                {
                    BaseUrl = "https://api.bitails.io",
                    Transport = BitailsRealtimeTransportMode.Websocket,
                    Websocket = new BitailsWebsocketConnectionConfig
                    {
                        BaseUrl = "https://api.bitails.io/global"
                    }
                }
            },
            Whatsonchain = new WhatsOnChainSourceConfig
            {
                Enabled = true,
                EnabledCapabilities =
                [
                    ExternalChainCapability.RawTxFetch,
                    ExternalChainCapability.ValidationFetch,
                    ExternalChainCapability.BlockBackfill
                ],
                Connection = new HttpApiSourceConnectionConfig
                {
                    BaseUrl = "https://api.whatsonchain.com/v1/bsv/main"
                }
            }
        };
}

public class SourceRoutingConfig
{
    public string PreferredMode { get; set; }
    public string PrimarySource { get; set; }
    public string[] FallbackSources { get; set; } = [];
    public string VerificationSource { get; set; }

    public static SourceRoutingConfig CreateDefaults()
        => new()
        {
            PreferredMode = "hybrid",
            PrimarySource = ExternalChainProviderName.Bitails,
            FallbackSources = [ExternalChainProviderName.JungleBus, "node"],
            VerificationSource = "node"
        };
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

    public static SourceCapabilitiesConfig CreateDefaults()
        => new()
        {
            Broadcast = new BroadcastCapabilityOverrideConfig
            {
                Mode = "multi",
                Sources = ["node", ExternalChainProviderName.Bitails]
            },
            RealtimeIngest = new RoutedCapabilityOverrideConfig
            {
                Source = ExternalChainProviderName.Bitails,
                FallbackSources = [ExternalChainProviderName.JungleBus, "node"]
            },
            BlockBackfill = new RoutedCapabilityOverrideConfig
            {
                Source = ExternalChainProviderName.JungleBus,
                FallbackSources = ["node"]
            },
            RawTxFetch = new RoutedCapabilityOverrideConfig
            {
                Source = ExternalChainProviderName.Bitails,
                FallbackSources = [ExternalChainProviderName.WhatsOnChain]
            },
            ValidationFetch = new RoutedCapabilityOverrideConfig
            {
                Source = "node",
                FallbackSources = [ExternalChainProviderName.Bitails]
            },
            HistoricalAddressScan = new RoutedCapabilityOverrideConfig
            {
                Source = ExternalChainProviderName.Bitails,
                FallbackSources = [ExternalChainProviderName.WhatsOnChain]
            },
            HistoricalTokenScan = new RoutedCapabilityOverrideConfig
            {
                Source = ExternalChainProviderName.Bitails
            }
        };
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
    public BitailsSourceConnectionConfig Connection { get; set; } = new();
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

public static class BitailsRealtimeTransportMode
{
    public const string Websocket = "websocket";
    public const string Zmq = "zmq";
}

public class BitailsSourceConnectionConfig : HttpApiSourceConnectionConfig
{
    public string Transport { get; set; }
    public BitailsWebsocketConnectionConfig Websocket { get; set; } = new();
    public BitailsZmqConnectionConfig Zmq { get; set; } = new();
}

public class BitailsWebsocketConnectionConfig
{
    public string BaseUrl { get; set; }
}

public class BitailsZmqConnectionConfig
{
    public string TxUrl { get; set; }
    public string BlockUrl { get; set; }
}
