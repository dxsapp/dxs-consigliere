using System.Linq;
using System.Reflection;

using Dxs.Bsv;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.WoC;

namespace Dxs.Consigliere.Tests.Broadcast;

/// <summary>
/// Wave 5 S6 — reflection-based grep regression. Pins the W5 done-when
/// at the build level: "grep shows no legacy broadcast HTTP-provider
/// paths". A re-introduction of any legacy broadcast method on the
/// audited interfaces fails this test instead of waiting for runtime
/// or another audit pass.
/// </summary>
public class BroadcastUnificationGrepTests
{
    [Fact]
    public void IBroadcastService_HasNoLegacyBroadcastOverloads()
    {
        var iface = typeof(IBroadcastService);
        var methods = iface.GetMethods();
        // The legacy Broadcast(string) and Broadcast(Transaction)
        // overloads return Task<Data.Models.Broadcast>; the unified
        // BroadcastAsync returns Task<BroadcastReceipt>.
        foreach (var m in methods)
        {
            // The only valid method name on this interface is
            // BroadcastAsync (or SatoshisPerByte).
            Assert.True(
                m.Name is "BroadcastAsync" or "SatoshisPerByte",
                $"Unexpected method on IBroadcastService: {m.Name}");
            // Pin: no method called "Broadcast" (without Async suffix).
            Assert.NotEqual("Broadcast", m.Name);
        }
    }

    [Fact]
    public void BroadcastService_HasNoLegacyBroadcastOverloads()
    {
        var type = typeof(BroadcastService);
        var publicBroadcastMethods = type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Broadcast")
            .ToArray();
        Assert.Empty(publicBroadcastMethods);
    }

    [Fact]
    public void IBroadcastProvider_TypeDoesNotExist()
    {
        // W5 S3: renamed to IFeeRateProvider; the old name must not
        // resurface.
        var bsvAsm = typeof(IFeeRateProvider).Assembly;
        var oldType = bsvAsm.GetType("Dxs.Bsv.IBroadcastProvider");
        Assert.Null(oldType);
    }

    [Fact]
    public void IFeeRateProvider_HasNoBroadcastMethod()
    {
        // Confirm the renamed interface is fee-only.
        var iface = typeof(IFeeRateProvider);
        var broadcast = iface.GetMethod("Broadcast");
        Assert.Null(broadcast);
        var satoshis = iface.GetMethod("SatoshisPerByte");
        Assert.NotNull(satoshis);
    }

    [Fact]
    public void IBitcoindService_HasNoBroadcastMethod()
    {
        // W5 S3: BitcoindService.Broadcast deleted. The interface
        // surface inherits from IFeeRateProvider — confirm via the
        // inherited interface absence.
        var iface = typeof(IBitcoindService);
        // GetMethods reports inherited via BindingFlags default. We want
        // to assert "Broadcast" isn't reachable through any inheritance.
        var broadcast = iface.GetMethod("Broadcast");
        Assert.Null(broadcast);
    }

    [Fact]
    public void IBitailsRestApiClient_HasNoBroadcastMethod()
    {
        var iface = typeof(IBitailsRestApiClient);
        var broadcast = iface.GetMethod("Broadcast");
        Assert.Null(broadcast);
    }

    [Fact]
    public void IWhatsOnChainRestApiClient_HasNoBroadcastAsyncMethod()
    {
        var iface = typeof(IWhatsOnChainRestApiClient);
        var broadcast = iface.GetMethod("BroadcastAsync");
        Assert.Null(broadcast);
    }
}
