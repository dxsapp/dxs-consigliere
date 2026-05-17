#nullable enable
namespace Dxs.Bsv.P2p.Chain;

/// <summary>
/// Wave 3 S1 — abstraction over the longest-chain rule. BSV uses
/// cumulative work, but headers as stored today don't carry per-header
/// work. W3 approximates with height (BSV difficulty is stable across
/// the 200-header retention window, so equal cumulative-work-per-block);
/// a future wave can swap in <see cref="WorkBitsComparer"/> without
/// re-shaping callers.
/// </summary>
public interface ICumulativeWorkComparer
{
    /// <summary>Standard <see cref="IComparable"/> semantics: negative
    /// if A &lt; B, zero on equal work, positive if A &gt; B.</summary>
    int Compare(long heightA, long heightB);
}

public sealed class HeightCumulativeWorkComparer : ICumulativeWorkComparer
{
    public int Compare(long heightA, long heightB) => heightA.CompareTo(heightB);
}
