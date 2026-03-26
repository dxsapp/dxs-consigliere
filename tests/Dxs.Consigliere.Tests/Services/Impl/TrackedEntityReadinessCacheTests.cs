using Dxs.Common.Cache;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Tracking;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using Microsoft.Extensions.DependencyInjection;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TrackedEntityReadinessCacheTests : RavenTestDriver
{
    private const string Address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";

    [Fact]
    public async Task AddressReadiness_RehydratesAfterLifecycleMutation()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        using var cacheServices = CreateCacheServices();

        var registration = new TrackedEntityRegistrationStore(
            store,
            cacheServices.GetRequiredService<IProjectionCacheInvalidationSink>(),
            cacheServices.GetRequiredService<IProjectionReadCacheKeyFactory>());
        var orchestrator = new TrackedEntityLifecycleOrchestrator(
            store,
            cacheServices.GetRequiredService<IProjectionCacheInvalidationSink>(),
            cacheServices.GetRequiredService<IProjectionReadCacheKeyFactory>());
        var readiness = new TrackedEntityReadinessService(
            store,
            cacheServices.GetRequiredService<IProjectionReadCache>(),
            cacheServices.GetRequiredService<IProjectionReadCacheKeyFactory>());

        await registration.RegisterAddressAsync(Address, "Genesis");

        var registered = await readiness.GetAddressReadinessAsync(Address);
        Assert.Equal("registered", registered.LifecycleStatus);
        Assert.False(registered.Readable);

        await orchestrator.BeginTrackingAddressAsync(Address);
        await orchestrator.MarkAddressBackfillCompletedAsync(Address);
        await orchestrator.MarkAddressGapClosedAsync(Address);

        var live = await readiness.GetAddressReadinessAsync(Address);
        Assert.Equal("live", live.LifecycleStatus);
        Assert.True(live.Readable);
        Assert.True(live.Authoritative);
        Assert.Equal(TrackedEntityHistoryReadiness.ForwardLive, live.History.HistoryReadiness);
    }

    private static ServiceProvider CreateCacheServices()
    {
        var services = new ServiceCollection();
        services.AddProjectionReadCache(options =>
        {
            options.MaxEntries = 128;
            options.DefaultSafetyTtl = TimeSpan.FromMinutes(5);
        });
        services.AddSingleton<IProjectionReadCacheKeyFactory, ProjectionReadCacheKeyFactory>();
        return services.BuildServiceProvider();
    }
}
