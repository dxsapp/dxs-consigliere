using Dxs.Common.Cache;
using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Tracking;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Moq;

namespace Dxs.Consigliere.Tests.Controllers;

public class AdminControllerTests
{
    [Fact]
    public async Task GetCacheStatus_ReturnsProjectionCacheMetrics()
    {
        var controller = new AdminController(new TestNetworkProvider());

        var result = await controller.GetCacheStatus(new FakeProjectionReadCacheTelemetry(), new FakeProjectionCacheRuntimeStatusReader());

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ProjectionCacheStatusResponse>(ok.Value);
        Assert.True(payload.Enabled);
        Assert.Equal("memory", payload.Backend);
        Assert.Equal(42, payload.Hits);
        Assert.Equal(9, payload.InvalidatedKeys);
        Assert.Equal(2, payload.Invalidation.Domains.Length);
        Assert.Equal(4, payload.ProjectionLag.Token.Lag);
        Assert.Equal(7, payload.HistoryEnvelopeBackfill.PendingCount);
    }

    [Fact]
    public async Task ManageStasToken_RejectsFullHistoryWithoutTrustedRoots()
    {
        var controller = new AdminController(new TestNetworkProvider());

        var result = await controller.ManageStasToken(
            new WatchStasTokenRequest(
                "1111111111111111111111111111111111111111",
                "ROOT",
                new HistoryPolicyRequest { Mode = HistoryPolicyMode.FullHistory }),
            Mock.Of<ITrackedEntityRegistrationStore>(MockBehavior.Strict),
            Mock.Of<ITrackedEntityLifecycleOrchestrator>(MockBehavior.Strict),
            Mock.Of<ITrackedHistoryBackfillScheduler>(MockBehavior.Strict),
            Mock.Of<ITrackedEntityReadinessService>(MockBehavior.Strict),
            Mock.Of<Dxs.Bsv.BitcoinMonitor.ITransactionFilter>(MockBehavior.Strict));

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task UpgradeTokensHistory_BulkRejectsMissingTrustedRootsPerItem()
    {
        var controller = new AdminController(new TestNetworkProvider());
        var readiness = new Mock<ITrackedEntityReadinessService>(MockBehavior.Strict);

        var result = await controller.UpgradeTokensHistory(
            new BulkTokenHistoryUpgradeRequest
            {
                Items =
                [
                    new TokenHistoryUpgradeRequest
                    {
                        TokenId = "1111111111111111111111111111111111111111",
                        TokenHistoryPolicy = new TokenHistoryPolicyRequest()
                    }
                ]
            },
            Mock.Of<ITrackedEntityRegistrationStore>(MockBehavior.Strict),
            Mock.Of<ITrackedEntityLifecycleOrchestrator>(MockBehavior.Strict),
            Mock.Of<ITrackedHistoryBackfillScheduler>(MockBehavior.Strict),
            readiness.Object,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<Dxs.Consigliere.Dto.Responses.History.BulkHistoryUpgradeResponse>(ok.Value);
        Assert.Single(payload.Items);
        Assert.False(payload.Items[0].Accepted);
        Assert.Equal("trusted_roots_required", payload.Items[0].MessageCode);
    }

    [Fact]
    public void GetStorageStatus_ReturnsPayloadStorageContract()
    {
        var controller = new AdminController(new TestNetworkProvider());

        var result = controller.GetStorageStatus(Options.Create(new Dxs.Consigliere.Configs.ConsigliereStorageConfig
        {
            RawTransactionPayloads =
            {
                Enabled = true,
                Provider = "raven",
                Location = new Dxs.Consigliere.Configs.RawTransactionPayloadLocationConfig
                {
                    Database = "consigliere-payloads",
                    Collection = "RawTransactions"
                },
                Compression = new Dxs.Consigliere.Configs.PayloadCompressionConfig
                {
                    Enabled = true,
                    Algorithm = "gzip"
                }
            }
        }));

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<StorageStatusResponse>(ok.Value);
        Assert.True(payload.RawTransactionPayloads.Enabled);
        Assert.True(payload.RawTransactionPayloads.ProviderImplemented);
        Assert.True(payload.RawTransactionPayloads.PersistenceActive);
        Assert.Equal("forever", payload.RawTransactionPayloads.RetentionPolicy);
        Assert.Equal("gzip", payload.RawTransactionPayloads.Compression);
        Assert.Equal("RawTransactions", payload.RawTransactionPayloads.Location.Collection);
    }

    private sealed class FakeProjectionReadCacheTelemetry : IProjectionReadCacheTelemetry
    {
        public ProjectionCacheStatsSnapshot GetSnapshot()
            => new(
                "memory",
                true,
                11,
                1024,
                42,
                18,
                21,
                9,
                4,
                2);
    }

    private sealed class TestNetworkProvider : Dxs.Consigliere.Services.INetworkProvider
    {
        public Dxs.Bsv.Network Network => Dxs.Bsv.Network.Mainnet;
    }

    private sealed class FakeProjectionCacheRuntimeStatusReader : IProjectionCacheRuntimeStatusReader
    {
        public Task<ProjectionCacheRuntimeStatusSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(
                new ProjectionCacheRuntimeStatusSnapshot(
                    new ProjectionLagSnapshot(
                        25,
                        new ProjectionLagItemSnapshot("address", 21, 4),
                        new ProjectionLagItemSnapshot("token", 21, 4),
                        new ProjectionLagItemSnapshot("tx_lifecycle", 24, 1)),
                    new ProjectionCacheInvalidationTelemetrySnapshot(
                        5,
                        11,
                        DateTimeOffset.Parse("2026-03-26T18:10:00+00:00"),
                        [
                            new ProjectionCacheInvalidationDomainSnapshot("address", 3, 7, DateTimeOffset.Parse("2026-03-26T18:10:00+00:00")),
                            new ProjectionCacheInvalidationDomainSnapshot("tracked_readiness", 2, 4, DateTimeOffset.Parse("2026-03-26T18:10:05+00:00"))
                        ]),
                    new AddressHistoryEnvelopeBackfillTelemetrySnapshot(
                        20,
                        13,
                        1,
                        7,
                        301,
                        DateTimeOffset.Parse("2026-03-26T18:11:00+00:00"),
                        DateTimeOffset.Parse("2026-03-26T18:11:03+00:00"))));
    }
}
