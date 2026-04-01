using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;
using Dxs.Tests.Shared;

using Microsoft.Extensions.Options;

using Raven.Client.Documents;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Runtime;

public class JungleBusChainTipAssuranceReaderTests : RavenTestDriver
{
    [Fact]
    public async Task GetSnapshotAsync_ReturnsUnavailable_WhenBlockBackfillPrimaryIsNotJungleBus()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var reader = CreateReader(store, CreateNodeFirstSources());

        var snapshot = await reader.GetSnapshotAsync();

        Assert.False(snapshot.Primary);
        Assert.Equal("unavailable", snapshot.State);
        Assert.Equal("unavailable", snapshot.AssuranceMode);
        Assert.Equal("block_backfill_primary_not_junglebus", snapshot.UnavailableReason);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsCatchingUpSingleSource_WhenLaggedButProgressing()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedAsync(store,
            localHeight: 151,
            health: new JungleBusBlockSyncHealthDocument
            {
                Id = JungleBusBlockSyncHealthDocument.DocumentId,
                LastObservedBlockHeight = 155,
                LastObservedMovementAt = DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeMilliseconds(),
                LastObservedMovementHeight = 155,
                LastControlMessageAt = DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeMilliseconds(),
                LastLocalProgressAt = DateTimeOffset.UtcNow.AddSeconds(-25).ToUnixTimeMilliseconds(),
                LastLocalProgressHeight = 151,
                LastProcessedAt = DateTimeOffset.UtcNow.AddSeconds(-25).ToUnixTimeMilliseconds(),
                LastProcessedBlockHeight = 151
            });

        var reader = CreateReader(store, CreateJungleBusFirstSources());
        var snapshot = await reader.GetSnapshotAsync();

        Assert.Equal("catching_up", snapshot.State);
        Assert.Equal("single_source", snapshot.AssuranceMode);
        Assert.True(snapshot.SingleSourceAssurance);
        Assert.Equal(4, snapshot.LagBlocks);
        Assert.False(snapshot.ControlFlowStalled);
        Assert.False(snapshot.LocalProgressStalled);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsStalledControlFlow_WhenControlMessagesAreStale()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedAsync(store,
            localHeight: 151,
            health: new JungleBusBlockSyncHealthDocument
            {
                Id = JungleBusBlockSyncHealthDocument.DocumentId,
                LastObservedBlockHeight = 155,
                LastObservedMovementAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds(),
                LastObservedMovementHeight = 155,
                LastControlMessageAt = DateTimeOffset.UtcNow.AddMinutes(-4).ToUnixTimeMilliseconds(),
                LastLocalProgressAt = DateTimeOffset.UtcNow.AddSeconds(-20).ToUnixTimeMilliseconds(),
                LastLocalProgressHeight = 151
            });

        var reader = CreateReader(store, CreateJungleBusFirstSources());
        var snapshot = await reader.GetSnapshotAsync();

        Assert.Equal("stalled_control_flow", snapshot.State);
        Assert.True(snapshot.ControlFlowStalled);
        Assert.False(snapshot.LocalProgressStalled);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsStalledLocalProgress_WhenTipAdvancesButLocalProgressStops()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedAsync(store,
            localHeight: 151,
            health: new JungleBusBlockSyncHealthDocument
            {
                Id = JungleBusBlockSyncHealthDocument.DocumentId,
                LastObservedBlockHeight = 160,
                LastObservedMovementAt = DateTimeOffset.UtcNow.AddSeconds(-20).ToUnixTimeMilliseconds(),
                LastObservedMovementHeight = 160,
                LastControlMessageAt = DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeMilliseconds(),
                LastLocalProgressAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds(),
                LastLocalProgressHeight = 151,
                LastProcessedAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds(),
                LastProcessedBlockHeight = 151
            });

        var reader = CreateReader(store, CreateJungleBusFirstSources());
        var snapshot = await reader.GetSnapshotAsync();

        Assert.Equal("stalled_local_progress", snapshot.State);
        Assert.False(snapshot.ControlFlowStalled);
        Assert.True(snapshot.LocalProgressStalled);
        Assert.Equal(9, snapshot.LagBlocks);
    }

    private static async Task SeedAsync(IDocumentStore store, int? localHeight, JungleBusBlockSyncHealthDocument health)
    {
        using var session = store.OpenAsyncSession();
        if (localHeight.HasValue)
        {
            await session.StoreAsync(new BlockProcessContext
            {
                Id = "blocks/known",
                Height = localHeight.Value
            });
        }

        await session.StoreAsync(health);
        await session.SaveChangesAsync();
    }

    private static JungleBusChainTipAssuranceReader CreateReader(IDocumentStore store, ConsigliereSourcesConfig config)
        => new(
            store,
            new FakeAdminProviderConfigService(config),
            new FakeAdminRuntimeSourcePolicyService(config),
            new FakeProviderCatalog(),
            new FakeProviderSettingsAccessor(config),
            Options.Create(CreateAppConfig()));

    private static ConsigliereSourcesConfig CreateJungleBusFirstSources()
    {
        var config = new ConsigliereSourcesConfig
        {
            Providers = SourceProvidersConfig.CreateDefaults(),
            Routing = SourceRoutingConfig.CreateDefaults(),
            Capabilities = SourceCapabilitiesConfig.CreateDefaults()
        };

        config.Capabilities.BlockBackfill.Source = ExternalChainProviderName.JungleBus;
        config.Capabilities.BlockBackfill.FallbackSources = [SourceCapabilityRouting.NodeProvider];
        config.Providers.JungleBus.Connection.BaseUrl = "https://junglebus.gorillapool.io";
        return config;
    }

    private static ConsigliereSourcesConfig CreateNodeFirstSources()
    {
        var config = CreateJungleBusFirstSources();
        config.Capabilities.BlockBackfill.Source = SourceCapabilityRouting.NodeProvider;
        return config;
    }

    private static AppConfig CreateAppConfig()
        => new()
        {
            JungleBus = new JungleBusConfig
            {
                Enabled = true,
                MempoolSubscriptionId = "mempool-sub",
                BlockSubscriptionId = "block-sub"
            }
        };

    private sealed class FakeAdminProviderConfigService(ConsigliereSourcesConfig config) : IAdminProviderConfigService
    {
        public Task<ConsigliereSourcesConfig> GetEffectiveSourcesConfigAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(config);

        public Task<JungleBusProviderRuntimeSnapshot> GetEffectiveJungleBusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new JungleBusProviderRuntimeSnapshot(
                config.Providers.JungleBus.Connection.BaseUrl,
                CreateAppConfig().JungleBus.MempoolSubscriptionId,
                CreateAppConfig().JungleBus.BlockSubscriptionId));

        public Task<Dto.Responses.Admin.AdminProvidersResponse> GetProvidersAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdminProviderConfigMutationResult> ApplyProviderConfigAsync(Dto.Requests.AdminProviderConfigUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Dto.Responses.Admin.AdminProvidersResponse> ResetProviderConfigAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeAdminRuntimeSourcePolicyService(ConsigliereSourcesConfig config) : IAdminRuntimeSourcePolicyService
    {
        public Task<ConsigliereSourcesConfig> GetEffectiveSourcesConfigAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(config);

        public Task<Dto.Responses.Admin.AdminRuntimeSourcesResponse> GetRuntimeSourcesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdminRealtimeSourcePolicyMutationResult> ApplyRealtimePolicyAsync(string primaryRealtimeSource, string bitailsTransport, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Dto.Responses.Admin.AdminRuntimeSourcesResponse> ResetRealtimePolicyAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeProviderCatalog : IExternalChainProviderCatalog
    {
        public IReadOnlyCollection<ExternalChainProviderDescriptor> GetDescriptors()
            =>
            [
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.JungleBus,
                    [ExternalChainCapability.BlockBackfill, ExternalChainCapability.RawTxFetch, ExternalChainCapability.RealtimeIngest])
            ];

        public Task<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>>([]);
    }

    private sealed class FakeProviderSettingsAccessor(ConsigliereSourcesConfig config) : IExternalChainProviderSettingsAccessor
    {
        public ValueTask<BitailsProviderRuntimeSettings> GetBitailsAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new BitailsProviderRuntimeSettings(null, null, "websocket", null, null, null));

        public ValueTask<WhatsOnChainProviderRuntimeSettings> GetWhatsOnChainAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new WhatsOnChainProviderRuntimeSettings(null, null));

        public ValueTask<JungleBusProviderRuntimeSettings> GetJungleBusAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new JungleBusProviderRuntimeSettings(
                config.Providers.JungleBus.Connection.BaseUrl,
                config.Providers.JungleBus.Connection.ApiKey,
                CreateAppConfig().JungleBus.MempoolSubscriptionId,
                CreateAppConfig().JungleBus.BlockSubscriptionId));
    }
}
