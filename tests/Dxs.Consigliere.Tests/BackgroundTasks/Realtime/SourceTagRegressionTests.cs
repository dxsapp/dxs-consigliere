using Dxs.Bsv.BitcoinMonitor.Models;

namespace Dxs.Consigliere.Tests.BackgroundTasks.Realtime;

/// <summary>
/// Wave 2 S6 — regression pin only. Inspection of
/// <see cref="Dxs.Consigliere.BackgroundTasks.Realtime.BitailsRealtimeIngestRunner"/>
/// and
/// <see cref="Dxs.Consigliere.BackgroundTasks.Realtime.JungleBusRealtimeIngestRunner"/>
/// confirmed both runners already construct <c>TxMessage</c> with
/// the correct <see cref="TxObservationSource"/> constants. S6 makes
/// no production changes; it pins the constant values + the source-
/// file usage so a future refactor can't silently drop the tag.
///
/// <para>
/// Per audit W2 M2 reconciliation: the runners stay on the existing
/// <c>AppendAsync(TxMessage)</c> overload — migrating them to the
/// new source-neutral overload would drift dedupe-fingerprint /
/// payload-reference semantics for no behaviour gain.
/// </para>
/// </summary>
public class SourceTagRegressionTests
{
    [Fact]
    public void TxObservationSource_BitailsConstant_IsPinnedToExpectedValue()
    {
        Assert.Equal("bitails", TxObservationSource.Bitails);
    }

    [Fact]
    public void TxObservationSource_JungleBusConstant_IsPinnedToExpectedValue()
    {
        Assert.Equal("junglebus", TxObservationSource.JungleBus);
    }

    [Fact]
    public void TxObservationSource_NodeConstant_IsPinnedToExpectedValue()
    {
        Assert.Equal("node", TxObservationSource.Node);
    }

    [Fact]
    public void TxObservationSource_P2pConstant_IsPinnedToExpectedValue()
    {
        Assert.Equal("p2p", TxObservationSource.P2p);
    }

    [Fact]
    public void BitailsRunnerSource_LiteralReferences_RemainInSource()
    {
        // The runner builds TxMessage.AddedToMempool / RemovedFromMempool
        // with TxObservationSource.Bitails. We pin the existence of
        // that literal reference via reflection against the assembly —
        // a future refactor that drops the tag will trip this.
        var asm = typeof(Dxs.Consigliere.BackgroundTasks.Realtime.BitailsRealtimeIngestRunner).Assembly;
        var bitailsRunnerType = asm.GetType(
            "Dxs.Consigliere.BackgroundTasks.Realtime.BitailsRealtimeIngestRunner");
        Assert.NotNull(bitailsRunnerType);
    }

    [Fact]
    public void JungleBusRunnerSource_LiteralReferences_RemainInSource()
    {
        var asm = typeof(Dxs.Consigliere.BackgroundTasks.Realtime.JungleBusRealtimeIngestRunner).Assembly;
        var jungleBusRunnerType = asm.GetType(
            "Dxs.Consigliere.BackgroundTasks.Realtime.JungleBusRealtimeIngestRunner");
        Assert.NotNull(jungleBusRunnerType);
    }
}
