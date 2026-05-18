using System;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Tests.Shared;

using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 6 S2 (S1+S2-audit M1) — integration pins for
/// <see cref="RavenAlertEventRepository"/>. The unit-level fake
/// repository can't prove the append-only invariant against a real
/// document store; this fixture spins up an embedded Raven via
/// <see cref="RavenTestDriver"/> and exercises the production path.
/// </summary>
public class RavenAlertEventRepositoryTests : RavenTestDriver
{
    static RavenAlertEventRepositoryTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false,
            },
        });
    }

    [SkippableFact]
    public async Task SaveAsync_DuplicateId_ThrowsAndDoesNotOverwrite()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");
        using var store = GetDocumentStore();
        var sut = new RavenAlertEventRepository(store);

        var alertUnixMs = 1700000000_000L;
        var original = new P2pAlertEvent
        {
            Id = P2pAlertEvent.BuildId(alertUnixMs),
            AlertUnixMs = alertUnixMs,
            Type = P2pAlertType.PoolSizeBelowThreshold,
            Detail = "original detail",
        };
        await sut.SaveAsync(original, default);

        // Attempt to save a SECOND alert with the same id but
        // mutated payload — the production path must refuse, and the
        // original must remain intact.
        var imposter = new P2pAlertEvent
        {
            Id = original.Id,
            AlertUnixMs = alertUnixMs,
            Type = P2pAlertType.ReorgDepthExceeded,
            Detail = "imposter detail",
        };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SaveAsync(imposter, default));
        Assert.Contains("id collision", ex.Message);

        // Read back: original detail still present, imposter not applied.
        var recent = await sut.GetRecentAsync(limit: 10, sinceUnixMs: null, default);
        var doc = Assert.Single(recent);
        Assert.Equal("original detail", doc.Detail);
        Assert.Equal(P2pAlertType.PoolSizeBelowThreshold, doc.Type);
    }

    [SkippableFact]
    public async Task SaveAsync_FreshId_PersistsSuccessfully()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");
        using var store = GetDocumentStore();
        var sut = new RavenAlertEventRepository(store);

        var alertUnixMs = 1700000001_000L;
        var ev = new P2pAlertEvent
        {
            Id = P2pAlertEvent.BuildId(alertUnixMs),
            AlertUnixMs = alertUnixMs,
            Type = P2pAlertType.RelayBackRateBelowThreshold,
            Detail = "rate 0.05 below 0.30",
        };
        await sut.SaveAsync(ev, default);

        var recent = await sut.GetRecentAsync(limit: 10, sinceUnixMs: null, default);
        var doc = Assert.Single(recent);
        Assert.Equal(ev.Id, doc.Id);
        Assert.Equal("rate 0.05 below 0.30", doc.Detail);
    }
}
