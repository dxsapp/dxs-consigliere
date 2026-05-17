namespace Dxs.Bsv.BitcoinMonitor.Models;

/// <summary>
/// Wave 3 S0 — canonical source values for <see cref="BlockObservation.Source"/>.
/// The field is a free-form string for historical reasons; W3 stabilises
/// the values so downstream code can discriminate (e.g. the projection
/// rebuilder identifies reorg-induced disconnects vs operator-forced
/// rescans). Parallel to <c>TxObservationSource</c>.
/// </summary>
public static class BlockObservationSource
{
    public const string Node = "node";
    public const string JungleBus = "junglebus";

    /// <summary>Wave 3: reorg-detector-emitted disconnect events.</summary>
    public const string Reorg = "reorg";
}
