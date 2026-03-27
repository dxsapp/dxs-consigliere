using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.Common;

namespace Dxs.Bsv.Tests.Bitails;

public class BitailsProviderDiagnosticsTests
{
    [Fact]
    public void Descriptor_AdvertisesRealtimeAndHistoricalBitailsCapabilities()
    {
        var diagnostics = new BitailsProviderDiagnostics();

        Assert.Equal(ExternalChainProviderName.Bitails, diagnostics.Descriptor.Provider);
        Assert.Contains(ExternalChainCapability.RealtimeIngest, diagnostics.Descriptor.Capabilities);
        Assert.Contains(ExternalChainCapability.RawTxFetch, diagnostics.Descriptor.Capabilities);
        Assert.Contains(ExternalChainCapability.ValidationFetch, diagnostics.Descriptor.Capabilities);
        Assert.Contains(ExternalChainCapability.HistoricalAddressScan, diagnostics.Descriptor.Capabilities);
        Assert.Contains(ExternalChainCapability.HistoricalTokenScan, diagnostics.Descriptor.Capabilities);
        Assert.Equal(600, diagnostics.Descriptor.RateLimitHint?.RequestsPerMinute);
    }
}
