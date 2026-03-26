using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Setup;

namespace Dxs.Consigliere.Tests.Setup;

public class VNextStartupDiagnosticsTests
{
    [Fact]
    public void Describe_FormatsRoutingProvidersAndPayloadStore()
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
                Bitails = new BitailsSourceConfig { Enabled = false },
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

        var lines = VNextStartupDiagnostics.Describe(sources, storage, VNextCutoverMode.ShadowRead);

        Assert.Contains(lines, x => x.Contains("VNext cutover mode: shadow_read"));
        Assert.Contains(lines, x => x.Contains("VNext routing mode: hybrid"));
        Assert.Contains(lines, x => x.Contains("VNext primary source: junglebus"));
        Assert.Contains(lines, x => x.Contains("node (broadcast, validation_fetch)"));
        Assert.Contains(lines, x => x.Contains("raven/(default-db)/RawTransactionPayloads"));
    }
}
