using System.Globalization;
using System.Threading.RateLimiting;
using Dxs.Consigliere.Configs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Consigliere.Setup;

public static class RateLimiterPolicies
{
    public const string LoginPolicy = "login";
    public const string MePolicy = "me";
    public const string BroadcastPolicy = "broadcast";

    /// <summary>
    /// wave-A3 S1 — registers three IP-keyed fixed-window
    /// policies via the built-in `Microsoft.AspNetCore.
    /// RateLimiting` middleware. The conservative defaults
    /// (`5 login/min`, `100 me/min`, `1 broadcast/sec`) come
    /// from <see cref="RateLimitingConfig"/>; the operator can
    /// loosen via `RateLimiting:*` config or env vars.
    ///
    /// Wave-A3 S0 wired `UseForwardedHeaders` before
    /// `UseRouting`, so the IP key here reads the original
    /// client address — not Caddy's loopback hop.
    ///
    /// On rejection: emit 429 + a `Retry-After` header (the
    /// frontend ApiClient honours it for a single retry).
    /// </summary>
    public static IServiceCollection AddConsigliereRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RateLimitingConfig>()
            .Bind(configuration.GetSection("RateLimiting"));

        services.AddRateLimiter(rl =>
        {
            rl.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rl.OnRejected = OnRejectedAsync;

            rl.AddPolicy(LoginPolicy, ResolveOptions(configuration).Login.ToPartitionFactory(LoginPolicy));
            rl.AddPolicy(MePolicy, ResolveOptions(configuration).Me.ToPartitionFactory(MePolicy));
            rl.AddPolicy(BroadcastPolicy, ResolveOptions(configuration).Broadcast.ToPartitionFactory(BroadcastPolicy));
        });

        return services;
    }

    private static RateLimitingConfig ResolveOptions(IConfiguration configuration)
    {
        var bound = configuration.GetSection("RateLimiting").Get<RateLimitingConfig>();
        return bound ?? new RateLimitingConfig();
    }

    /// <summary>
    /// Returns the bucket factory the policy uses to partition
    /// counters by client IP. <paramref name="policyName"/> is
    /// folded into the partition key so an attacker hitting
    /// /login does not consume /me's allowance.
    /// </summary>
    internal static Func<HttpContext, RateLimitPartition<string>> ToPartitionFactory(
        this RateLimitPolicyConfig policy,
        string policyName)
    {
        var window = policy.PermitsPerSecond > 0
            ? TimeSpan.FromSeconds(1)
            : TimeSpan.FromMinutes(1);
        var permitLimit = policy.PermitsPerSecond > 0
            ? policy.PermitsPerSecond
            : Math.Max(1, policy.PermitsPerMinute);
        var queueLimit = Math.Max(0, policy.QueueLimit);

        return httpContext =>
        {
            // Forwarded-headers middleware rewrites this to the
            // real client IP (wave-A3 S0). Fall back to a single
            // "anonymous" partition if the framework cannot
            // resolve the remote address — better than letting
            // an unkeyed request bypass the limit entirely.
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"{policyName}:{ip}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = permitLimit,
                    QueueLimit = queueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    Window = window,
                });
        };
    }

    private static ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        // The lease metadata carries the recommended retry-
        // after delay; surface it via the standard HTTP header
        // so a polite client can pace itself. Round up to a
        // whole second per RFC 7231.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }
        return ValueTask.CompletedTask;
    }
}
