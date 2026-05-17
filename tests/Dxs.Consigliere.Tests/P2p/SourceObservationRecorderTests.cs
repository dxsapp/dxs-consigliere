using System.Threading.Tasks;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Consigliere.Services.P2p;

namespace Dxs.Consigliere.Tests.P2p;

public class SourceObservationRecorderTests
{
    [Fact]
    public void RecordInvObserved_AggregatesBySource()
    {
        var r = new SourceObservationRecorder();
        r.RecordInvObserved(TxObservationSource.P2p);
        r.RecordInvObserved(TxObservationSource.P2p);
        r.RecordInvObserved(TxObservationSource.Bitails);

        Assert.Equal(2, r.GetInvObservedCount(TxObservationSource.P2p));
        Assert.Equal(1, r.GetInvObservedCount(TxObservationSource.Bitails));
        Assert.Equal(0, r.GetInvObservedCount(TxObservationSource.JungleBus));
    }

    [Fact]
    public void RecordInvObserved_NullOrEmptySource_FallsBackToP2p()
    {
        var r = new SourceObservationRecorder();
        r.RecordInvObserved("");
        r.RecordInvObserved(null!);
        Assert.Equal(2, r.GetInvObservedCount(TxObservationSource.P2p));
    }

    [Fact]
    public void CounterMethods_AreIndependent()
    {
        var r = new SourceObservationRecorder();
        r.RecordMatched();
        r.RecordMatched();
        r.RecordUnmatched();
        r.RecordParseError();
        r.RecordRateLimited();
        r.RecordGetDataTimeout();
        r.RecordOversizePayload();

        Assert.Equal(2, r.GetMatchedCount());
        Assert.Equal(1, r.GetUnmatchedCount());
        Assert.Equal(1, r.GetParseErrorCount());
        Assert.Equal(1, r.GetRateLimitedCount());
        Assert.Equal(1, r.GetGetDataTimeoutCount());
        Assert.Equal(1, r.GetOversizePayloadCount());
    }

    [Fact]
    public async Task Increment_IsThreadSafe()
    {
        var r = new SourceObservationRecorder();
        const int N = 10_000;
        var t1 = Task.Run(() => { for (var i = 0; i < N; i++) r.RecordMatched(); });
        var t2 = Task.Run(() => { for (var i = 0; i < N; i++) r.RecordMatched(); });
        var t3 = Task.Run(() => { for (var i = 0; i < N; i++) r.RecordInvObserved(TxObservationSource.P2p); });
        var t4 = Task.Run(() => { for (var i = 0; i < N; i++) r.RecordInvObserved(TxObservationSource.P2p); });
        await Task.WhenAll(t1, t2, t3, t4);

        Assert.Equal(2 * N, r.GetMatchedCount());
        Assert.Equal(2 * N, r.GetInvObservedCount(TxObservationSource.P2p));
    }
}
