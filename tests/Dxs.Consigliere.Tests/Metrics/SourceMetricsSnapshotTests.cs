using Dxs.Consigliere.Data.Models.Metrics;

namespace Dxs.Consigliere.Tests.Metrics;

/// <summary>
/// Wave 4 S0 — pins the document shape, bucket boundary mathematics,
/// and id format. These are W4's frozen contracts (Core Rule §2 + §4
/// in the wave master.md) and must remain stable for W6 alarm rules
/// + future external Prometheus / Grafana exporters.
/// </summary>
public class SourceMetricsSnapshotTests
{
    [Fact]
    public void SourceMetricsBuckets_Count_IsSix()
    {
        // Frozen contract.
        Assert.Equal(6, SourceMetricsBuckets.Count);
        Assert.Equal(5, SourceMetricsBuckets.UpperBoundsMs.Count);
    }

    [Fact]
    public void SourceMetricsBuckets_UpperBounds_AreDocumentedValues()
    {
        // Frozen: <10ms, 10-50ms, 50-200ms, 200ms-1s, 1s-5s, >5s.
        Assert.Equal(new long[] { 10, 50, 200, 1_000, 5_000 },
            SourceMetricsBuckets.UpperBoundsMs.ToArray());
    }

    [Fact]
    public void SourceMetricsBuckets_UpperBounds_ExposedAsReadOnlyType()
    {
        // A2 M1 fix: the static public type is IReadOnlyList<long>,
        // not long[]. Callers writing through the property will hit
        // a compile error (an IReadOnlyList<long> has no indexer
        // setter). Pin via reflection so a future refactor to long[]
        // fails the build, not the audit.
        var prop = typeof(SourceMetricsBuckets).GetProperty(
            nameof(SourceMetricsBuckets.UpperBoundsMs));
        Assert.NotNull(prop);
        Assert.Equal(
            typeof(System.Collections.Generic.IReadOnlyList<long>),
            prop!.PropertyType);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(49, 1)]
    [InlineData(50, 2)]
    [InlineData(199, 2)]
    [InlineData(200, 3)]
    [InlineData(999, 3)]
    [InlineData(1_000, 4)]
    [InlineData(4_999, 4)]
    [InlineData(5_000, 5)]
    [InlineData(60_000, 5)]
    public void IndexFor_PicksCorrectBucket(long lagMs, int expectedIndex)
    {
        Assert.Equal(expectedIndex, SourceMetricsBuckets.IndexFor(lagMs));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(-1000, 0)]
    [InlineData(long.MinValue, 0)]
    public void IndexFor_NegativeLag_ClampsToFirstBucket(long lagMs, int expectedIndex)
    {
        // Core Rule §5: clock skew → bucket 0.
        Assert.Equal(expectedIndex, SourceMetricsBuckets.IndexFor(lagMs));
    }

    [Fact]
    public void BuildId_PadsTimestampForLexicographicOrder()
    {
        // The d14 padding makes Raven's lexicographic ORDER BY return
        // snapshots in time order (lowest unix-ms first). Pin the
        // exact format so the admin endpoint + retention eviction
        // can both rely on it.
        Assert.Equal("metrics/sources/00000000000000",
            SourceMetricsBuckets.BuildId(0));
        Assert.Equal("metrics/sources/00000000000123",
            SourceMetricsBuckets.BuildId(123));
        Assert.Equal("metrics/sources/01700000000000",
            SourceMetricsBuckets.BuildId(1_700_000_000_000));
    }

    [Fact]
    public void SnapshotDefaults_ContainersInitialized()
    {
        // Defensive: a fresh snapshot should have non-null containers
        // so the aggregator + admin DTOs can rely on them.
        var snap = new SourceMetricsSnapshot();
        Assert.NotNull(snap.ObservationCounters);
        Assert.NotNull(snap.VisibilityCounters);
        Assert.NotNull(snap.Rebroadcast);
        Assert.Null(snap.LastDegradedReorgAt);
    }

    [Fact]
    public void VisibilityCounters_LagBuckets_DefaultsToSixZeros()
    {
        var v = new SourceVisibilityCounters();
        Assert.Equal(SourceMetricsBuckets.Count, v.LagBuckets.Length);
        Assert.All(v.LagBuckets, x => Assert.Equal(0L, x));
    }
}
