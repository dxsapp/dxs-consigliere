using System.Linq;
using System.Reflection;

using Dxs.Consigliere.Services;

namespace Dxs.Consigliere.Tests.Broadcast;

/// <summary>
/// Wave 5 S0 — pin the canonical `IBroadcastService` surface as it
/// evolves through the wave. After S0 the interface still carries
/// the legacy `Broadcast` overloads (deleted in S2); the new
/// canonical method is `BroadcastAsync` (renamed from the prior
/// `SubmitAsync`). This test fails the build if either rename is
/// reverted or the legacy methods get re-added after S2.
/// </summary>
public class IBroadcastServiceShapeTests
{
    [Fact]
    public void Interface_HasBroadcastAsync_NotSubmitAsync()
    {
        var iface = typeof(IBroadcastService);
        var broadcastAsync = iface.GetMethod(nameof(IBroadcastService.BroadcastAsync));
        Assert.NotNull(broadcastAsync);

        // The legacy SubmitAsync name must NOT exist anymore.
        var submitAsync = iface.GetMethod("SubmitAsync");
        Assert.Null(submitAsync);
    }

    [Fact]
    public void Interface_BroadcastAsync_ReturnsBroadcastReceipt()
    {
        var iface = typeof(IBroadcastService);
        var method = iface.GetMethod(nameof(IBroadcastService.BroadcastAsync));
        Assert.NotNull(method);
        // Task<BroadcastReceipt>.
        var ret = method!.ReturnType;
        Assert.True(ret.IsGenericType);
        Assert.Equal(typeof(System.Threading.Tasks.Task<>), ret.GetGenericTypeDefinition());
        Assert.Equal(typeof(BroadcastReceipt), ret.GetGenericArguments()[0]);
    }

    [Fact]
    public void Interface_BroadcastAsync_SignatureIsCanonical()
    {
        // wave-A3 S3-followup-2: a required `BroadcastSource source`
        // was threaded in as p[1] so the audit trail records honest
        // provenance instead of a hardcoded "admin-ui".
        // (string rawHex, BroadcastSource source,
        //  string clientConnectionId = null, CancellationToken ct = default)
        var method = typeof(IBroadcastService).GetMethod(nameof(IBroadcastService.BroadcastAsync));
        Assert.NotNull(method);
        var p = method!.GetParameters();
        Assert.Equal(4, p.Length);
        Assert.Equal("rawHex", p[0].Name);
        Assert.Equal(typeof(string), p[0].ParameterType);
        Assert.Equal("source", p[1].Name);
        Assert.Equal(typeof(BroadcastSource), p[1].ParameterType);
        Assert.False(p[1].HasDefaultValue); // required — callers must declare provenance
        Assert.Equal("clientConnectionId", p[2].Name);
        Assert.True(p[2].HasDefaultValue);
        Assert.Equal("ct", p[3].Name);
        Assert.Equal(typeof(System.Threading.CancellationToken), p[3].ParameterType);
    }
}
