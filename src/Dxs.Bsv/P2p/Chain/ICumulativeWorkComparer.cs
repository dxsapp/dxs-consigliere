#nullable enable
using System.Collections.Generic;
using System.Numerics;

using Dxs.Bsv.P2p.Messages;

namespace Dxs.Bsv.P2p.Chain;

/// <summary>
/// Wave 3 — comparison rule for fork promotion. Implementations
/// receive the active chain side and the fork side (each as a list of
/// <see cref="BlockHeader"/> from common-ancestor+1 up to the tip) and
/// return standard <see cref="System.IComparable"/> semantics: negative
/// when fork &lt; active (no promotion), zero on equal work (first-seen
/// = active wins), positive when fork &gt; active (promote).
///
/// <para>Audit W3 A2 C1 fix: the original W3 draft used
/// height-as-cumulative-work which a malicious peer could trivially
/// game by minting low-difficulty headers (each header carries its own
/// <c>bits</c>; <see cref="HeadersChain.TryExtend"/> only checks PoW
/// against that field, not against the expected BSV
/// difficulty-adjustment rule). The default implementation now is
/// <see cref="WorkBitsCumulativeWorkComparer"/> which integrates the
/// per-header work approximation (1 / target) and compares cumulative
/// work across the two chains.</para>
/// </summary>
public interface ICumulativeWorkComparer
{
    int Compare(IReadOnlyList<BlockHeader> activeChainSide, IReadOnlyList<BlockHeader> forkChainSide);
}

/// <summary>
/// Production-default <see cref="ICumulativeWorkComparer"/>. Per-header
/// work is computed as <c>2^256 / (target + 1)</c> where <c>target</c>
/// is the expanded compact-bits value (see
/// <see cref="BlockHeaderHasher.TryExpandTarget"/>). A header with an
/// invalid bits field contributes zero work.
/// </summary>
public sealed class WorkBitsCumulativeWorkComparer : ICumulativeWorkComparer
{
    private static readonly BigInteger TwoTo256 = BigInteger.One << 256;

    public int Compare(IReadOnlyList<BlockHeader> activeChainSide, IReadOnlyList<BlockHeader> forkChainSide)
    {
        var active = TotalWork(activeChainSide);
        var fork = TotalWork(forkChainSide);
        return fork.CompareTo(active);
    }

    private static BigInteger TotalWork(IReadOnlyList<BlockHeader> headers)
    {
        var total = BigInteger.Zero;
        for (var i = 0; i < headers.Count; i++)
            total += HeaderWork(headers[i]);
        return total;
    }

    /// <summary>Approximate per-header work: <c>2^256 / (target + 1)</c>.
    /// Returns zero when the header's <c>bits</c> field is malformed.</summary>
    public static BigInteger HeaderWork(BlockHeader header)
    {
        var bits = BlockHeaderHasher.Bits(header);
        if (!BlockHeaderHasher.TryExpandTarget(bits, out var targetWire))
            return BigInteger.Zero;
        // targetWire is little-endian per the W1 contract. BigInteger
        // ctor `isBigEndian: false` reads LE; isUnsigned: true keeps the
        // top bit from being interpreted as a sign.
        var target = new BigInteger(targetWire, isUnsigned: true, isBigEndian: false);
        if (target.IsZero) return BigInteger.Zero;
        return TwoTo256 / (target + BigInteger.One);
    }
}

/// <summary>
/// Vestigial height-only comparer retained for tests that don't care
/// about chainwork (e.g. the S6 fixtures where every header is mined
/// at the same regtest difficulty so cumulative work tracks height
/// exactly). Production uses
/// <see cref="WorkBitsCumulativeWorkComparer"/>.
/// </summary>
public sealed class HeightCumulativeWorkComparer : ICumulativeWorkComparer
{
    public int Compare(IReadOnlyList<BlockHeader> activeChainSide, IReadOnlyList<BlockHeader> forkChainSide)
        => forkChainSide.Count.CompareTo(activeChainSide.Count);
}
