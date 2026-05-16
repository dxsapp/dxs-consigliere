using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Pool;
using Dxs.Bsv.P2p.Session;

namespace Dxs.Bsv.Tests.P2p.Session;

public class PeerManagerTests
{
    private static VersionMessage ClientVersion() =>
        new(
            ProtocolVersion: 70016,
            Services: 0x25,
            TimestampUnixSeconds: 1700000000L,
            AddrRecv: P2pAddress.Anonymous(0x25),
            AddrFrom: P2pAddress.Anonymous(0x25),
            Nonce: 1UL,
            UserAgent: "/test:0.1/",
            StartHeight: 0,
            Relay: true,
            AssociationId: null);

    [Fact]
    public async Task PeerDiscovery_AddSeed_PopulatesStore()
    {
        var store = new InMemoryPeerStore();
        var discovery = new PeerDiscovery(P2pNetwork.Mainnet, store);

        await discovery.AddSeedAsync(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 8333), CancellationToken.None);

        var record = await store.GetAsync(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 8333), CancellationToken.None);
        Assert.NotNull(record);
        Assert.Equal(PeerSource.Config, record!.Source);
    }

    [Fact]
    public async Task PeerDiscovery_RecordConnectAttempt_SuccessBumpsCounters()
    {
        var store = new InMemoryPeerStore();
        var discovery = new PeerDiscovery(P2pNetwork.Mainnet, store);
        var ep = new IPEndPoint(IPAddress.Parse("1.2.3.4"), 8333);

        await discovery.AddSeedAsync(ep, CancellationToken.None);
        await discovery.RecordConnectAttemptAsync(ep, success: true, failureReason: null, TimeSpan.FromMinutes(15), CancellationToken.None);

        var record = await store.GetAsync(ep, CancellationToken.None);
        Assert.NotNull(record);
        Assert.Equal(1, record!.SuccessCount);
        Assert.NotNull(record.LastConnectedUtc);
        Assert.Null(record.NegativeUntilUtc);
    }

    [Fact]
    public async Task PeerDiscovery_RecordConnectAttempt_FailureSetsNegativeCooldown()
    {
        var store = new InMemoryPeerStore();
        var discovery = new PeerDiscovery(P2pNetwork.Mainnet, store);
        var ep = new IPEndPoint(IPAddress.Parse("1.2.3.4"), 8333);

        await discovery.AddSeedAsync(ep, CancellationToken.None);
        await discovery.RecordConnectAttemptAsync(ep, success: false, failureReason: "peer closed", TimeSpan.FromMinutes(10), CancellationToken.None);

        var record = await store.GetAsync(ep, CancellationToken.None);
        Assert.NotNull(record);
        Assert.Equal(1, record!.FailCount);
        Assert.NotNull(record.NegativeUntilUtc);
        Assert.True(record.NegativeUntilUtc > DateTime.UtcNow);
        Assert.Equal("peer closed", record.LastFailureReason);
    }

    [Fact]
    public async Task SelectCandidates_ExcludesBannedAndAlreadyInPool()
    {
        var store = new InMemoryPeerStore();
        var ep1 = new IPEndPoint(IPAddress.Parse("1.1.1.1"), 8333);
        var ep2 = new IPEndPoint(IPAddress.Parse("2.2.2.2"), 8333);
        var ep3 = new IPEndPoint(IPAddress.Parse("3.3.3.3"), 8333);

        await store.UpsertAsync(new PeerRecord(ep1), CancellationToken.None);
        await store.UpsertAsync(new PeerRecord(ep2) { NegativeUntilUtc = DateTime.UtcNow.AddMinutes(10) }, CancellationToken.None);
        await store.UpsertAsync(new PeerRecord(ep3), CancellationToken.None);

        var excludeKeys = new HashSet<string> { $"{ep3.Address}:{ep3.Port}" };
        var got = await store.SelectCandidatesAsync(10, excludeKeys, new HashSet<string>(), CancellationToken.None);

        Assert.Single(got);
        Assert.Equal(ep1.Address, got[0].EndPoint.Address);
    }

    [Fact]
    public async Task SelectCandidates_ExcludesOverlapping24Subnets()
    {
        var store = new InMemoryPeerStore();
        await store.UpsertAsync(new PeerRecord(new IPEndPoint(IPAddress.Parse("10.0.0.5"), 8333)), CancellationToken.None);
        await store.UpsertAsync(new PeerRecord(new IPEndPoint(IPAddress.Parse("10.0.0.99"), 8333)), CancellationToken.None);
        await store.UpsertAsync(new PeerRecord(new IPEndPoint(IPAddress.Parse("11.0.0.1"), 8333)), CancellationToken.None);

        var excludeSubnets = new HashSet<string> { "10.0.0/24" };
        var got = await store.SelectCandidatesAsync(10, new HashSet<string>(), excludeSubnets, CancellationToken.None);

        Assert.Single(got);
        Assert.Equal("11.0.0.1", got[0].EndPoint.Address.ToString());
    }

    [Fact]
    public async Task PeerManager_ConnectsToOneSeedPeer_ReachesPoolSize1()
    {
        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();

        var store = new InMemoryPeerStore();
        var discovery = new PeerDiscovery(P2pNetwork.Mainnet, store);
        await discovery.AddSeedAsync(server.EndPoint, CancellationToken.None);

        var config = new PeerManagerConfig
        {
            TargetPoolSize = 1,
            BootstrapJitter = TimeSpan.Zero,
            MaintenanceInterval = TimeSpan.FromMilliseconds(200),
            DnsRefreshInterval = TimeSpan.FromHours(1),
            VersionFactory = ClientVersion,
            SessionConfig = new PeerSessionConfig { HandshakeTimeout = TimeSpan.FromSeconds(2), ConnectTimeout = TimeSpan.FromSeconds(2) },
            EnableFallbackSeeds = false,
        };

        await using var manager = new PeerManager(P2pNetwork.Mainnet, discovery, store, config);
        await manager.StartAsync(CancellationToken.None);

        await WaitUntil(() => manager.PoolSize == 1, TimeSpan.FromSeconds(5));
        Assert.Equal(1, manager.PoolSize);

        // Store should have recorded the successful connection.
        var record = await store.GetAsync(server.EndPoint, CancellationToken.None);
        Assert.NotNull(record);
        Assert.True(record!.SuccessCount >= 1);
        Assert.Equal("/MiniBsvServer:0.1/", record.UserAgent);
    }

    [Fact]
    public async Task PeerManager_PoolSize_DoesNotExceedTarget()
    {
        // Spin up three independent servers; target=2, expect manager to use only two of them.
        await using var s1 = new MiniBsvServer(P2pNetwork.Mainnet);
        await using var s2 = new MiniBsvServer(P2pNetwork.Mainnet);
        await using var s3 = new MiniBsvServer(P2pNetwork.Mainnet);
        await s1.StartAsync();
        await s2.StartAsync();
        await s3.StartAsync();

        var store = new InMemoryPeerStore();
        var discovery = new PeerDiscovery(P2pNetwork.Mainnet, store);
        await discovery.AddSeedAsync(s1.EndPoint, CancellationToken.None);
        await discovery.AddSeedAsync(s2.EndPoint, CancellationToken.None);
        await discovery.AddSeedAsync(s3.EndPoint, CancellationToken.None);

        var config = new PeerManagerConfig
        {
            TargetPoolSize = 2,
            BootstrapJitter = TimeSpan.Zero,
            MaintenanceInterval = TimeSpan.FromMilliseconds(200),
            DnsRefreshInterval = TimeSpan.FromHours(1),
            VersionFactory = ClientVersion,
            SessionConfig = new PeerSessionConfig { HandshakeTimeout = TimeSpan.FromSeconds(2), ConnectTimeout = TimeSpan.FromSeconds(2) },
            EnableFallbackSeeds = false,
        };

        await using var manager = new PeerManager(P2pNetwork.Mainnet, discovery, store, config);
        await manager.StartAsync(CancellationToken.None);

        await WaitUntil(() => manager.PoolSize == 2, TimeSpan.FromSeconds(5));

        await Task.Delay(500); // Give it time to over-shoot if logic is wrong.
        Assert.Equal(2, manager.PoolSize);
    }

    [Fact]
    public async Task PeerManager_FailureRecordsNegativeCooldown()
    {
        // Use an unroutable peer; manager must record a failure with cooldown.
        var unroutable = new IPEndPoint(IPAddress.Parse("240.0.0.1"), 8333);

        var store = new InMemoryPeerStore();
        var discovery = new PeerDiscovery(P2pNetwork.Mainnet, store);
        await discovery.AddSeedAsync(unroutable, CancellationToken.None);

        var config = new PeerManagerConfig
        {
            TargetPoolSize = 1,
            BootstrapJitter = TimeSpan.Zero,
            MaintenanceInterval = TimeSpan.FromMilliseconds(200),
            NegativeCooldown = TimeSpan.FromMinutes(10),
            VersionFactory = ClientVersion,
            SessionConfig = new PeerSessionConfig { ConnectTimeout = TimeSpan.FromMilliseconds(500), HandshakeTimeout = TimeSpan.FromSeconds(1) },
            EnableFallbackSeeds = false,
        };

        await using var manager = new PeerManager(P2pNetwork.Mainnet, discovery, store, config);
        await manager.StartAsync(CancellationToken.None);

        await WaitUntil(async () =>
        {
            var rec = await store.GetAsync(unroutable, CancellationToken.None);
            return rec is not null && rec.FailCount >= 1;
        }, TimeSpan.FromSeconds(5));

        var record = await store.GetAsync(unroutable, CancellationToken.None);
        Assert.NotNull(record);
        Assert.True(record!.FailCount >= 1);
        Assert.NotNull(record.NegativeUntilUtc);
    }

    private static async Task WaitUntil(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(25);
        }
    }

    private static async Task WaitUntil(Func<Task<bool>> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await predicate()) return;
            await Task.Delay(25);
        }
    }
}
