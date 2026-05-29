using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Data.Runtime;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Data.Runtime;

public class OperatorRuntimeSettingsServiceTests
{
    [Fact]
    public async Task GetP2pEnabled_NoDocument_FallsBackToConfigSeed_False()
    {
        var service = Create(store: new FakeStore(null), configEnabled: false);
        Assert.False(await service.GetP2pEnabledAsync());
    }

    [Fact]
    public async Task GetP2pEnabled_NoDocument_FallsBackToConfigSeed_True()
    {
        var service = Create(store: new FakeStore(null), configEnabled: true);
        Assert.True(await service.GetP2pEnabledAsync());
    }

    [Fact]
    public async Task GetP2pEnabled_DocumentOverridesConfigSeed_TrueOverFalse()
    {
        var doc = new OperatorRuntimeSettingsDocument { P2pEnabled = true };
        var service = Create(store: new FakeStore(doc), configEnabled: false);
        Assert.True(await service.GetP2pEnabledAsync());
    }

    [Fact]
    public async Task GetP2pEnabled_DocumentOverridesConfigSeed_FalseOverTrue()
    {
        var doc = new OperatorRuntimeSettingsDocument { P2pEnabled = false };
        var service = Create(store: new FakeStore(doc), configEnabled: true);
        Assert.False(await service.GetP2pEnabledAsync());
    }

    [Fact]
    public async Task GetP2pEnabled_DocumentWithNullField_FallsBackToConfigSeed()
    {
        var doc = new OperatorRuntimeSettingsDocument { P2pEnabled = null };
        var service = Create(store: new FakeStore(doc), configEnabled: true);
        Assert.True(await service.GetP2pEnabledAsync());
    }

    [Fact]
    public async Task SetP2pEnabled_PersistsValueAndUpdatedBy()
    {
        var store = new FakeStore(null);
        var service = Create(store, configEnabled: false);

        await service.SetP2pEnabledAsync(true, "alice");

        Assert.NotNull(store.Saved);
        Assert.True(store.Saved!.P2pEnabled);
        Assert.Equal("alice", store.Saved.UpdatedBy);
        // round-trips: a subsequent read reflects the persisted override
        Assert.True(await service.GetP2pEnabledAsync());
    }

    [Fact]
    public async Task SetP2pEnabled_BlankUpdatedBy_DefaultsToSystem()
    {
        var store = new FakeStore(null);
        var service = Create(store, configEnabled: false);

        await service.SetP2pEnabledAsync(true, "  ");

        Assert.Equal("system", store.Saved!.UpdatedBy);
    }

    private static OperatorRuntimeSettingsService Create(FakeStore store, bool configEnabled)
        => new(store, Options.Create(new BsvP2pConfig { Enabled = configEnabled }));

    private sealed class FakeStore(OperatorRuntimeSettingsDocument seed) : IOperatorRuntimeSettingsStore
    {
        private OperatorRuntimeSettingsDocument _current = seed;
        public OperatorRuntimeSettingsDocument Saved { get; private set; }

        public Task<OperatorRuntimeSettingsDocument> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_current);

        public Task<OperatorRuntimeSettingsDocument> SaveAsync(
            OperatorRuntimeSettingsDocument document,
            CancellationToken cancellationToken = default)
        {
            Saved = document;
            _current = document;
            return Task.FromResult(document);
        }
    }
}
