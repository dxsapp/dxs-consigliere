using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Chain;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.P2p;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Controllers;

/// <summary>
/// Wave 6 S3 — controller tests for
/// <c>GET /api/admin/p2p/alerts</c>. Pins the clamp helper +
/// default lastN, since-filter, descending order, retention-bounded
/// behaviour, and frozen DTO shape.
/// </summary>
public class AdminP2pControllerAlertsTests
{
    [Theory]
    [InlineData(null, 720, 20)]          // omitted → DefaultAlertLastN (20)
    [InlineData(0, 720, 20)]             // non-positive → default
    [InlineData(-5, 720, 20)]            // negative → default
    [InlineData(50, 720, 50)]            // below retention → unclamped
    [InlineData(720, 720, 720)]          // equal retention → unclamped
    [InlineData(721, 720, 720)]          // above retention → clamped
    [InlineData(5000, 720, 720)]         // far above retention → clamped
    [InlineData(int.MaxValue, 720, 720)] // extreme → retention wins
    [InlineData(50, 0, 50)]              // retention=0 → fall back to hard ceiling
    [InlineData(1500, 0, 1440)]          // retention=0 + above ceiling → ceiling wins
    [InlineData(1500, 2000, 1440)]       // retention > ceiling + far-above → ceiling wins
    public void ClampAlertLastN_HelperReturnsExpectedBoundedValue(int? requestedLastN, int retention, int expectedBounded)
    {
        var bounded = AdminP2pController.ClampAlertLastN(requestedLastN, retention);
        Assert.Equal(expectedBounded, bounded);
    }

    [Fact]
    public void HardCeilingConstant_IsDocumentedValue()
    {
        Assert.Equal(1440, AdminP2pController.HardAlertLastNCeiling);
        Assert.Equal(20, AdminP2pController.DefaultAlertLastN);
    }

    [Fact]
    public async Task GetAlerts_OmittedLastN_ReturnsDefaultPage()
    {
        var (controller, repo) = Build(retention: 720);
        SeedAlerts(repo, count: 50);

        var result = await controller.GetAlerts(lastN: null, since: null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<P2pAlertResponse>(ok.Value);
        Assert.Equal(AdminP2pController.DefaultAlertLastN, response.Alerts.Count);
    }

    [Fact]
    public async Task GetAlerts_DescendingOrder_NewestFirst()
    {
        var (controller, repo) = Build();
        SeedAlerts(repo, count: 5);

        var result = await controller.GetAlerts(lastN: 5, since: null, CancellationToken.None);

        var response = Assert.IsType<P2pAlertResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(5, response.Alerts.Count);
        // Repo's GetRecentAsync emits descending; the controller
        // preserves that order in the response. Pin: first DTO is
        // the newest seeded id.
        var ms = response.Alerts.Select(a => a.AlertUnixMs).ToArray();
        Assert.True(ms.Zip(ms.Skip(1), (a, b) => a >= b).All(x => x));
    }

    [Fact]
    public async Task GetAlerts_SinceFilter_FiltersByUnixMs()
    {
        var (controller, repo) = Build();
        SeedAlerts(repo, count: 10);                // ids 1..10
        var cutoff = 5L;

        var result = await controller.GetAlerts(lastN: 100, since: cutoff, CancellationToken.None);

        var response = Assert.IsType<P2pAlertResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.All(response.Alerts, a => Assert.True(a.AlertUnixMs > cutoff));
        Assert.Equal(5, response.Alerts.Count); // ms 6..10
    }

    [Fact]
    public async Task GetAlerts_RetentionConstrainsResponse()
    {
        // Configured retention overrides a far-too-large lastN.
        var (controller, repo) = Build(retention: 3);
        SeedAlerts(repo, count: 50);

        var result = await controller.GetAlerts(lastN: 100, since: null, CancellationToken.None);

        var response = Assert.IsType<P2pAlertResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(3, response.Alerts.Count);
    }

    [Fact]
    public async Task GetAlerts_DtoShape_PreservesEvaluatorFields()
    {
        var (controller, repo) = Build();
        await repo.SaveAsync(new P2pAlertEvent
        {
            Id = P2pAlertEvent.BuildId(1000L),
            AlertUnixMs = 1000L,
            Type = P2pAlertType.RelayBackRateBelowThreshold,
            Detail = "rate 0.05 below 0.30",
            Context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rate"] = "0.050000",
                ["threshold"] = "0.300",
            },
        }, CancellationToken.None);

        var result = await controller.GetAlerts(lastN: 1, since: null, CancellationToken.None);

        var response = Assert.IsType<P2pAlertResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        var dto = Assert.Single(response.Alerts);
        Assert.Equal("p2p/alerts/00000000001000", dto.Id);
        Assert.Equal(1000L, dto.AlertUnixMs);
        Assert.Equal("RelayBackRateBelowThreshold", dto.Type);
        Assert.Equal("rate 0.05 below 0.30", dto.Detail);
        Assert.Equal("0.050000", dto.Context["rate"]);
    }

    // -------- helpers --------

    private static (AdminP2pController controller, FakeAlertRepository repo) Build(int retention = 720)
    {
        var repo = new FakeAlertRepository();
        var controller = new AdminP2pController(
            new BsvP2pHealth(),
            // Headers store is not exercised here; an unused stub satisfies
            // the ctor. The tests we touch never hit headers/* endpoints.
            null!,
            Options.Create(new HeadersChainOptions()),
            repo,
            Options.Create(new BsvP2pConfig
            {
                Alert = new AlertConfig { AlertRetentionEvents = retention },
            }));
        return (controller, repo);
    }

    private static void SeedAlerts(FakeAlertRepository repo, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            repo.Stored[P2pAlertEvent.BuildId(i)] = new P2pAlertEvent
            {
                Id = P2pAlertEvent.BuildId(i),
                AlertUnixMs = i,
                Type = P2pAlertType.PoolSizeBelowThreshold,
                Detail = $"seed {i}",
            };
        }
    }

    private sealed class FakeAlertRepository : IAlertEventRepository
    {
        public readonly ConcurrentDictionary<string, P2pAlertEvent> Stored = new();

        public Task SaveAsync(P2pAlertEvent ev, CancellationToken ct)
        {
            Stored[ev.Id] = ev;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetAllIdsOrderedAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(
                Stored.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList());

        public Task DeleteAsync(IReadOnlyList<string> ids, CancellationToken ct)
        {
            foreach (var id in ids) Stored.TryRemove(id, out _);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<P2pAlertEvent>> GetRecentAsync(
            int limit, long? sinceUnixMs, CancellationToken ct)
        {
            var hits = Stored.Values
                .Where(e => !sinceUnixMs.HasValue || e.AlertUnixMs > sinceUnixMs.Value)
                .OrderByDescending(e => e.Id)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<P2pAlertEvent>>(hits);
        }
    }
}
