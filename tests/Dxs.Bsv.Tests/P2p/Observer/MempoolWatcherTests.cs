using System;

using Dxs.Bsv.P2p.Observer;

namespace Dxs.Bsv.Tests.P2p.Observer;

public class MempoolWatcherTests
{
    [Fact]
    public void DecideFetch_FreshTxid_ReturnsFetch()
    {
        var w = new MempoolWatcher(new MempoolWatcherOptions());
        Assert.Equal(MempoolWatcher.FetchDecision.Fetch, w.DecideFetch("tx-1"));
    }

    [Fact]
    public void DecideFetch_DuplicateTxid_ReturnsDuplicate()
    {
        var w = new MempoolWatcher(new MempoolWatcherOptions());
        Assert.Equal(MempoolWatcher.FetchDecision.Fetch, w.DecideFetch("tx-1"));
        Assert.Equal(MempoolWatcher.FetchDecision.Duplicate, w.DecideFetch("tx-1"));
        Assert.Equal(MempoolWatcher.FetchDecision.Duplicate, w.DecideFetch("tx-1"));
        Assert.Equal(1, w.DedupeSize);
    }

    [Fact]
    public void DecideFetch_DifferentTxids_BothFetched()
    {
        var w = new MempoolWatcher(new MempoolWatcherOptions());
        Assert.Equal(MempoolWatcher.FetchDecision.Fetch, w.DecideFetch("tx-a"));
        Assert.Equal(MempoolWatcher.FetchDecision.Fetch, w.DecideFetch("tx-b"));
        Assert.Equal(2, w.DedupeSize);
    }

    [Fact]
    public void Forget_AllowsRefetchOfSameTxid()
    {
        var w = new MempoolWatcher(new MempoolWatcherOptions());
        Assert.Equal(MempoolWatcher.FetchDecision.Fetch, w.DecideFetch("tx-1"));
        Assert.Equal(MempoolWatcher.FetchDecision.Duplicate, w.DecideFetch("tx-1"));

        w.Forget("tx-1");

        Assert.Equal(MempoolWatcher.FetchDecision.Fetch, w.DecideFetch("tx-1"));
    }

    [Fact]
    public void RateLimit_FullWindow_ReturnsRateLimited()
    {
        // 3 getdatas/s allowed.
        var w = new MempoolWatcher(new MempoolWatcherOptions { MaxGetDataPerSec = 3 });

        Assert.Equal(MempoolWatcher.FetchDecision.Fetch, w.DecideFetch("tx-1"));
        Assert.Equal(MempoolWatcher.FetchDecision.Fetch, w.DecideFetch("tx-2"));
        Assert.Equal(MempoolWatcher.FetchDecision.Fetch, w.DecideFetch("tx-3"));
        // 4th unique txid in the same 1-second window → rate-limited
        Assert.Equal(MempoolWatcher.FetchDecision.RateLimited, w.DecideFetch("tx-4"));
    }

    [Fact]
    public void RateLimited_TxidNotPermanentlyBlocked()
    {
        // RateLimited result still records the txid in the dedupe
        // cache (because DecideFetch records "seen" before the
        // rate-limit check). The runner can Forget to retry.
        var w = new MempoolWatcher(new MempoolWatcherOptions { MaxGetDataPerSec = 1 });
        Assert.Equal(MempoolWatcher.FetchDecision.Fetch, w.DecideFetch("tx-1"));
        Assert.Equal(MempoolWatcher.FetchDecision.RateLimited, w.DecideFetch("tx-2"));
        w.Forget("tx-2");
        // Still rate-limited within the same window.
        Assert.Equal(MempoolWatcher.FetchDecision.RateLimited, w.DecideFetch("tx-2"));
    }

    [Fact]
    public void DecideFetch_EmptyOrNullTxid_ReturnsDuplicate()
    {
        var w = new MempoolWatcher(new MempoolWatcherOptions());
        Assert.Equal(MempoolWatcher.FetchDecision.Duplicate, w.DecideFetch(""));
        Assert.Equal(MempoolWatcher.FetchDecision.Duplicate, w.DecideFetch(null!));
    }

    [Fact]
    public void DedupeCap_TriggersEviction()
    {
        var w = new MempoolWatcher(new MempoolWatcherOptions
        {
            DedupeMaxEntries = 100,
            DedupeTtlSeconds = 1,
            MaxGetDataPerSec = 10_000,
        });

        // Fill the cache.
        for (var i = 0; i < 100; i++) w.DecideFetch($"tx-{i}");
        Assert.Equal(100, w.DedupeSize);

        // Adding the next entry should not push count over the cap
        // (eviction kicks in opportunistically).
        w.DecideFetch("tx-overflow");
        Assert.True(w.DedupeSize <= 100,
            $"DedupeSize={w.DedupeSize} exceeds cap of 100");
    }
}
