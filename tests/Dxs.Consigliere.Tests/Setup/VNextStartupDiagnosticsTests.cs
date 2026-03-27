using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Setup;

namespace Dxs.Consigliere.Tests.Setup;

public class VNextStartupDiagnosticsTests
{
    [Fact]
    public void Describe_FormatsRoutingProvidersPayloadStoreAndCache()
    {
        var sources = new ConsigliereSourcesConfig
        {
            Routing = new SourceRoutingConfig
            {
                PreferredMode = "hybrid",
                PrimarySource = "junglebus",
                FallbackSources = ["bitails", "node"],
                VerificationSource = "node"
            },
            Providers = new SourceProvidersConfig
            {
                Node = new NodeSourceConfig { Enabled = true, EnabledCapabilities = ["broadcast", "validation_fetch"] },
                JungleBus = new JungleBusSourceConfig { Enabled = true, EnabledCapabilities = ["realtime_ingest"] },
                Bitails = new BitailsSourceConfig
                {
                    Enabled = true,
                    EnabledCapabilities = ["realtime_ingest", "raw_tx_fetch"],
                    Connection =
                    {
                        Transport = BitailsRealtimeTransportMode.Websocket
                    }
                },
                Whatsonchain = new WhatsOnChainSourceConfig { Enabled = false }
            }
        };
        var storage = new ConsigliereStorageConfig
        {
            RawTransactionPayloads = new RawTransactionPayloadsStorageConfig
            {
                Enabled = true,
                Provider = "raven",
                Location = new RawTransactionPayloadLocationConfig
                {
                    Collection = "RawTransactionPayloads"
                }
            }
        };
        var cache = new ConsigliereCacheConfig
        {
            Enabled = true,
            Backend = "memory",
            MaxEntries = 4096
        };

        var lines = VNextStartupDiagnostics.Describe(sources, storage, cache, VNextCutoverMode.ShadowRead);

        Assert.Contains(lines, x => x.Contains("VNext cutover mode: shadow_read"));
        Assert.Contains(lines, x => x.Contains("VNext routing mode: hybrid"));
        Assert.Contains(lines, x => x.Contains("VNext primary source: junglebus"));
        Assert.Contains(lines, x => x.Contains("bitails[websocket] (realtime_ingest, raw_tx_fetch)"));
        Assert.Contains(lines, x => x.Contains("node (broadcast, validation_fetch)"));
        Assert.Contains(lines, x => x.Contains("raven/(default-db)/RawTransactionPayloads"));
        Assert.Contains(lines, x => x.Contains("memory/4096"));
    }
}
