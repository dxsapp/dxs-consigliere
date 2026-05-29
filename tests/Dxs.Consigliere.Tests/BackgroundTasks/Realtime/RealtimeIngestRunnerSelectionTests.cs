using Dxs.Consigliere.BackgroundTasks.Realtime;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

namespace Dxs.Consigliere.Tests.BackgroundTasks.Realtime;

/// <summary>
/// thin-node-primary-source S3 (Product Decision 2 / Core Rules 3-4):
/// making <c>p2p</c> the realtime primary must NOT silence the external
/// realtime runners. <see cref="RealtimeIngestBackgroundTask"/> selects
/// the set of external runners to drive purely from the effective
/// sources config (enabled + RealtimeIngest capability), independent of
/// which provider is the routed primary and independent of P2P peer
/// health — so a cold P2P pool can never reduce the external runner set.
/// </summary>
public class RealtimeIngestRunnerSelectionTests
{
    private static ConsigliereSourcesConfig DefaultSources()
        => new()
        {
            Providers = SourceProvidersConfig.CreateDefaults(),
            Routing = SourceRoutingConfig.CreateDefaults(),
            Capabilities = SourceCapabilitiesConfig.CreateDefaults()
        };

    [Fact]
    public void Selects_BothExternalRunners_WhenP2pIsPrimary_AndBothEnabled()
    {
        var sources = DefaultSources();

        // Default config: p2p is the realtime primary (Routing +
        // RealtimeIngest capability override both seed p2p).
        Assert.Equal(ExternalChainProviderName.P2p, sources.Routing.PrimarySource);
        Assert.Equal(ExternalChainProviderName.P2p, sources.Capabilities.RealtimeIngest.Source);

        var runners = RealtimeIngestBackgroundTask.ResolveEnabledExternalRunners(sources);

        // Both external realtime runners are scheduled, NOT just the
        // primary — the primary (p2p) runs as its own always-on hosted
        // service and is intentionally absent from this set.
        Assert.Contains(ExternalChainProviderName.Bitails, runners);
        Assert.Contains(ExternalChainProviderName.JungleBus, runners);
        Assert.DoesNotContain(ExternalChainProviderName.P2p, runners);
        Assert.DoesNotContain(SourceCapabilityRouting.NodeProvider, runners);
        Assert.Equal(2, runners.Count);
    }

    [Fact]
    public void Selection_IsIndependentOfPrimary()
    {
        // Even if a fallback/external provider is the routed primary, the
        // selected runner SET is identical — selection keys off enabled +
        // capability, never off the "primary" label. (Primary stays the
        // attribution/quorum anchor only.)
        var p2pPrimary = DefaultSources();

        var bitailsPrimary = DefaultSources();
        bitailsPrimary.Routing.PrimarySource = ExternalChainProviderName.Bitails;
        bitailsPrimary.Capabilities.RealtimeIngest.Source = ExternalChainProviderName.Bitails;

        var a = RealtimeIngestBackgroundTask.ResolveEnabledExternalRunners(p2pPrimary);
        var b = RealtimeIngestBackgroundTask.ResolveEnabledExternalRunners(bitailsPrimary);

        Assert.Equal(a, b);
    }

    [Fact]
    public void ColdP2pPool_DoesNotChangeRunnerSet()
    {
        // Runner selection takes no P2P-health input. A cold pool (0 ready
        // peers) cannot remove any external runner: with p2p primary and
        // BsvP2pConfig effectively dark, the external runners still feed
        // the journal. Proven structurally — the selector has no health
        // dependency to gate on.
        var sources = DefaultSources();

        var runners = RealtimeIngestBackgroundTask.ResolveEnabledExternalRunners(sources);

        Assert.Contains(ExternalChainProviderName.Bitails, runners);
        Assert.Contains(ExternalChainProviderName.JungleBus, runners);
    }

    [Fact]
    public void DisablingAProvider_DropsItsRunner()
    {
        var sources = DefaultSources();
        sources.Providers.JungleBus.Enabled = false;

        var runners = RealtimeIngestBackgroundTask.ResolveEnabledExternalRunners(sources);

        Assert.Contains(ExternalChainProviderName.Bitails, runners);
        Assert.DoesNotContain(ExternalChainProviderName.JungleBus, runners);
    }

    [Fact]
    public void DroppingRealtimeCapability_DropsItsRunner()
    {
        var sources = DefaultSources();
        sources.Providers.Bitails.EnabledCapabilities =
        [
            ExternalChainCapability.RawTxFetch,
            ExternalChainCapability.ValidationFetch
        ];

        var runners = RealtimeIngestBackgroundTask.ResolveEnabledExternalRunners(sources);

        Assert.DoesNotContain(ExternalChainProviderName.Bitails, runners);
        Assert.Contains(ExternalChainProviderName.JungleBus, runners);
    }
}
