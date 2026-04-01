using Dxs.Infrastructure.Common;
using Dxs.Infrastructure.JungleBus;

namespace Dxs.Bsv.Tests.JungleBus;

public class JungleBusProviderDiagnosticsTests
{
    [Fact]
    public void Descriptor_AdvertisesValidationFetchAndThrottledRawTxPolicy()
    {
        var diagnostics = new JungleBusProviderDiagnostics();

        Assert.Equal(ExternalChainProviderName.JungleBus, diagnostics.Descriptor.Provider);
        Assert.Contains(ExternalChainCapability.RawTxFetch, diagnostics.Descriptor.Capabilities);
        Assert.Contains(ExternalChainCapability.ValidationFetch, diagnostics.Descriptor.Capabilities);
        Assert.Contains(ExternalChainCapability.RealtimeIngest, diagnostics.Descriptor.Capabilities);
        Assert.Contains(ExternalChainCapability.BlockBackfill, diagnostics.Descriptor.Capabilities);
        Assert.Equal(600, diagnostics.Descriptor.RateLimitHint?.RequestsPerMinute);
        Assert.Equal("reverse_lineage_validation_fetch_10_per_second", diagnostics.Descriptor.RateLimitHint?.SourceHint);
    }
}
