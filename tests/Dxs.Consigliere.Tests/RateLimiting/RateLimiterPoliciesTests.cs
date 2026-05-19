using System.Threading.RateLimiting;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Setup;
using Microsoft.AspNetCore.Http;

namespace Dxs.Consigliere.Tests.RateLimiting;

/// <summary>
/// wave-A3 S1 — pins the per-policy partition-key shape +
/// the per-policy permit ceiling under burst.
///
/// The internal layout of <c>RateLimitPartition&lt;T&gt;</c>
/// changes between framework releases (no public getter for
/// the factory), so we verify policy behaviour by attempting
/// permit acquisition directly against the limiter the
/// factory produces.
/// </summary>
public sealed class RateLimiterPoliciesTests
{
    [Fact]
    public void PerSecond_policy_admits_exactly_PermitLimit_and_then_blocks()
    {
        using var limiter = BuildLimiter(new RateLimitPolicyConfig { PermitsPerSecond = 1 });
        Assert.True(limiter.AttemptAcquire().IsAcquired);
        Assert.False(limiter.AttemptAcquire().IsAcquired);
    }

    [Fact]
    public void PerMinute_policy_admits_PermitLimit_in_one_burst()
    {
        using var limiter = BuildLimiter(new RateLimitPolicyConfig { PermitsPerMinute = 5 });
        for (var i = 0; i < 5; i++)
            Assert.True(limiter.AttemptAcquire().IsAcquired, $"permit {i + 1} should pass");
        Assert.False(limiter.AttemptAcquire().IsAcquired);
    }

    [Fact]
    public void Partition_keys_are_namespaced_per_policy()
    {
        // Same IP, two policies: keys MUST diverge — otherwise
        // a brute-force login burst would also exhaust /me's
        // quota for that IP.
        var ip = ClientIp("10.20.30.40");
        var loginPartition = BuildPartition(
            new RateLimitPolicyConfig { PermitsPerMinute = 5 }, "login", ip);
        var mePartition = BuildPartition(
            new RateLimitPolicyConfig { PermitsPerMinute = 100 }, "me", ip);

        Assert.NotEqual(loginPartition.PartitionKey, mePartition.PartitionKey);
        Assert.StartsWith("login:", loginPartition.PartitionKey);
        Assert.StartsWith("me:", mePartition.PartitionKey);
    }

    [Fact]
    public void Partition_keys_include_remote_ip()
    {
        var partition = BuildPartition(
            new RateLimitPolicyConfig { PermitsPerMinute = 5 },
            "login",
            ClientIp("203.0.113.42"));
        Assert.Equal("login:203.0.113.42", partition.PartitionKey);
    }

    [Fact]
    public void Missing_remote_ip_falls_back_to_anonymous_partition()
    {
        // If the framework cannot resolve the remote address,
        // we must NOT bypass the limit entirely. A single
        // "unknown" partition is the conservative choice —
        // worse for false-positives, safer against abuse.
        var partition = BuildPartition(
            new RateLimitPolicyConfig { PermitsPerMinute = 5 }, "login", new DefaultHttpContext());
        Assert.Equal("login:unknown", partition.PartitionKey);
    }

    [Fact]
    public void PerSecond_takes_precedence_over_PerMinute()
    {
        // Defensive: if an operator sets both for the same
        // policy, the broadcast-style per-second rate wins.
        // 2 permits/sec means burst-3 should reject the 3rd.
        using var limiter = BuildLimiter(new RateLimitPolicyConfig
        {
            PermitsPerMinute = 999,
            PermitsPerSecond = 2,
        });
        Assert.True(limiter.AttemptAcquire().IsAcquired);
        Assert.True(limiter.AttemptAcquire().IsAcquired);
        Assert.False(limiter.AttemptAcquire().IsAcquired);
    }

    private static RateLimitPartition<string> BuildPartition(
        RateLimitPolicyConfig policy,
        string name,
        HttpContext context)
    {
        var factory = policy.ToPartitionFactory(name);
        return factory(context);
    }

    // Builds the actual limiter the partition would create by
    // routing through a single-key `PartitionedRateLimiter`.
    private static PartitionedRateLimiter<string> BuildLimiter(RateLimitPolicyConfig policy)
    {
        var factory = policy.ToPartitionFactory("test");
        var ip = ClientIp("198.51.100.1");
        return PartitionedRateLimiter.Create<string, string>(_ => factory(ip));
    }

    private static HttpContext ClientIp(string addr)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(addr);
        return ctx;
    }
}

internal static class PartitionedRateLimiterAcquireExtensions
{
    // Wraps the `AttemptAcquire(...)` call so the test can call
    // it without a resource key (single-partition limiter).
    public static RateLimitLease AttemptAcquire(this PartitionedRateLimiter<string> limiter)
        => limiter.AttemptAcquire("key");
}
