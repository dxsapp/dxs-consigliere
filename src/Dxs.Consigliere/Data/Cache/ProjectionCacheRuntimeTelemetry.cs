#nullable enable

using System.Collections.Concurrent;

using Dxs.Common.Cache;

namespace Dxs.Consigliere.Data.Cache;

public interface IProjectionCacheInvalidationTelemetry
{
    void Record(IEnumerable<ProjectionCacheTag> tags);
    ProjectionCacheInvalidationTelemetrySnapshot GetSnapshot();
}

public sealed class ProjectionCacheInvalidationTelemetry : IProjectionCacheInvalidationTelemetry
{
    private readonly ConcurrentDictionary<string, ProjectionCacheInvalidationDomainCounter> _domains = new(StringComparer.OrdinalIgnoreCase);
    private long _calls;
    private long _tags;
    private long _lastInvalidatedAtTicks;

    public void Record(IEnumerable<ProjectionCacheTag> tags)
    {
        if (tags is null)
            return;

        var normalized = tags
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (normalized.Length == 0)
            return;

        Interlocked.Increment(ref _calls);
        Interlocked.Add(ref _tags, normalized.Length);
        Interlocked.Exchange(ref _lastInvalidatedAtTicks, DateTimeOffset.UtcNow.UtcTicks);

        foreach (var domainGroup in normalized.GroupBy(ClassifyDomain, StringComparer.OrdinalIgnoreCase))
        {
            var counter = _domains.GetOrAdd(domainGroup.Key, static domain => new ProjectionCacheInvalidationDomainCounter(domain));
            counter.Record(domainGroup.Count());
        }
    }

    public ProjectionCacheInvalidationTelemetrySnapshot GetSnapshot()
        => new(
            Volatile.Read(ref _calls),
            Volatile.Read(ref _tags),
            ReadTimestamp(_lastInvalidatedAtTicks),
            _domains.Values
                .OrderBy(x => x.Domain, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.ToSnapshot())
                .ToArray());

    private static string ClassifyDomain(string tag)
    {
        if (tag.StartsWith("address-", StringComparison.OrdinalIgnoreCase))
            return "address";

        if (tag.StartsWith("token-", StringComparison.OrdinalIgnoreCase))
            return "token";

        if (tag.StartsWith("tx-lifecycle:", StringComparison.OrdinalIgnoreCase))
            return "tx_lifecycle";

        if (tag.StartsWith("tracked-address-readiness:", StringComparison.OrdinalIgnoreCase)
            || tag.StartsWith("tracked-token-readiness:", StringComparison.OrdinalIgnoreCase))
            return "tracked_readiness";

        return "other";
    }

    private static DateTimeOffset? ReadTimestamp(long ticks)
        => ticks <= 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);

    private sealed class ProjectionCacheInvalidationDomainCounter(string domain)
    {
        private long _calls;
        private long _tags;
        private long _lastInvalidatedAtTicks;

        public string Domain { get; } = domain;

        public void Record(int tagCount)
        {
            Interlocked.Increment(ref _calls);
            Interlocked.Add(ref _tags, tagCount);
            Interlocked.Exchange(ref _lastInvalidatedAtTicks, DateTimeOffset.UtcNow.UtcTicks);
        }

        public ProjectionCacheInvalidationDomainSnapshot ToSnapshot()
            => new(
                Domain,
                Volatile.Read(ref _calls),
                Volatile.Read(ref _tags),
                ReadTimestamp(Volatile.Read(ref _lastInvalidatedAtTicks)));
    }
}

public sealed record ProjectionCacheInvalidationTelemetrySnapshot(
    long Calls,
    long Tags,
    DateTimeOffset? LastInvalidatedAt,
    IReadOnlyCollection<ProjectionCacheInvalidationDomainSnapshot> Domains
);

public sealed record ProjectionCacheInvalidationDomainSnapshot(
    string Domain,
    long Calls,
    long Tags,
    DateTimeOffset? LastInvalidatedAt
);
