#nullable enable
using System;
using System.Collections.Generic;

using Dxs.Bsv.P2p.Messages;

namespace Dxs.Bsv.P2p.Chain;

/// <summary>
/// Wave 3 S1 — pure logic that inspects a <see cref="HeadersChain"/> and
/// a competing fork-side tip header and produces a <see cref="ReorgPlan"/>
/// (or <c>null</c> if no promotion is warranted). No I/O.
///
/// <para>Promotion rule (Core Rule §4): fork tip height &gt; active tip
/// height — equal-height ties go to the active chain (first-seen rule).
/// The <see cref="ICumulativeWorkComparer"/> dependency hides the
/// height-as-work approximation so a future wave can swap in a real
/// cumulative-work comparator.</para>
///
/// <para>Degraded path (Core Rule §4 + §9): when the walk back from the
/// fork tip exits the retained-header window before meeting the active
/// chain, the plan is marked <see cref="ReorgPlan.IsDegraded"/> and the
/// orphan / new-chain lists are empty. The emitter fires a single
/// <c>OnReorg(DegradedState = true)</c> and stops.</para>
/// </summary>
public sealed class ReorgDetector
{
    private readonly ICumulativeWorkComparer _comparer;

    public ReorgDetector(ICumulativeWorkComparer? comparer = null)
    {
        _comparer = comparer ?? new HeightCumulativeWorkComparer();
    }

    /// <summary>
    /// Attempt to detect a reorg given the current chain state and a
    /// fork-side candidate tip. The fork tip MUST already be stored in
    /// <paramref name="chain"/> (i.e. <see cref="HeadersChain.TryExtend"/>
    /// returned <see cref="ExtendResult.Fork"/> for it, recording it in
    /// the retained window). Returns <c>null</c> when no plan is
    /// warranted (chain empty, fork tip unknown, equal-height tie, or
    /// fork tip not actually higher).
    /// </summary>
    public ReorgPlan? TryDetect(HeadersChain chain, BlockHeader forkTip)
    {
        if (chain is null) throw new ArgumentNullException(nameof(chain));
        if (forkTip is null) throw new ArgumentNullException(nameof(forkTip));
        if (chain.Tip is null) return null;

        var activeTipWireHash = BlockHeaderHasher.Hash(chain.Tip);
        var forkTipWireHash = BlockHeaderHasher.Hash(forkTip);
        var forkTipWireHex = Hex(forkTipWireHash);

        // The fork tip might equal the active tip (no fork at all).
        if (string.Equals(Hex(activeTipWireHash), forkTipWireHex, StringComparison.Ordinal))
            return null;

        // Resolve the fork tip's height from the chain — TryExtend must
        // have stored it. If absent, the caller hasn't fed this header
        // through the chain yet; nothing to detect.
        if (!chain.TryGetByWireHashHex(forkTipWireHex, out _, out var forkTipHeight))
            return null;

        // Equal-height tie-break: active wins (first-seen rule).
        var comparison = _comparer.Compare(forkTipHeight, chain.TipHeight);
        if (comparison <= 0) return null;

        // Walk the active chain back from the tip via prev_block links,
        // building a hash set + ordered list (tip first). Stops when the
        // prev hash is not in the retained window (we've exited the
        // chain's memory).
        var activeChainWireHashes = new HashSet<string>(StringComparer.Ordinal);
        var activeChainOrdered = new List<(string WireHex, long Height)>();
        {
            var cursor = chain.Tip;
            var cursorHeight = chain.TipHeight;
            while (true)
            {
                var hex = Hex(BlockHeaderHasher.Hash(cursor));
                activeChainWireHashes.Add(hex);
                activeChainOrdered.Add((hex, cursorHeight));

                var prevWireHex = Hex(BlockHeaderHasher.PrevBlock(cursor));
                if (!chain.TryGetByWireHashHex(prevWireHex, out var prevHeader, out var prevHeight))
                    break;

                cursor = prevHeader;
                cursorHeight = prevHeight;
            }
        }

        // Walk back from the fork tip via prev_block until either:
        //   (a) we find a hash in the active-chain set → common ancestor;
        //   (b) we step out of the retained window → degraded.
        var forkSideWireHexes = new List<string>(); // newest-first

        var fCursor = forkTip;
        var fCursorWireHex = forkTipWireHex;
        var fCursorHeight = forkTipHeight;
        var stepsWalked = 0;

        while (true)
        {
            // Hit the active chain?
            if (activeChainWireHashes.Contains(fCursorWireHex))
            {
                // The current fork-side hex IS the common ancestor.
                // Everything we accumulated before this iteration is
                // fork-only; everything in active-chain at height >
                // common-ancestor-height is orphaned.
                var commonAncestorWireHex = fCursorWireHex;
                var commonAncestorHeight = fCursorHeight;

                // Build OrphanedHashes (display order, newest-first /
                // disconnect order).
                var orphaned = new List<string>();
                foreach (var (activeHex, activeHeight) in activeChainOrdered)
                {
                    if (activeHeight <= commonAncestorHeight) break;
                    orphaned.Add(WireHexToDisplayHex(activeHex));
                }

                // Build NewChainHashes (display order, oldest-first /
                // connect order). forkSideWireHexes is newest-first;
                // reverse + convert.
                var newChain = new List<string>(forkSideWireHexes.Count);
                for (var i = forkSideWireHexes.Count - 1; i >= 0; i--)
                {
                    newChain.Add(WireHexToDisplayHex(forkSideWireHexes[i]));
                }

                return new ReorgPlan(
                    CommonAncestorHash: WireHexToDisplayHex(commonAncestorWireHex),
                    CommonAncestorHeight: commonAncestorHeight,
                    OrphanedHashes: orphaned,
                    NewChainHashes: newChain,
                    NewTipHash: WireHexToDisplayHex(forkTipWireHex),
                    NewTipHeight: forkTipHeight,
                    IsDegraded: false);
            }

            // Record the current fork-side hex as part of the new chain.
            forkSideWireHexes.Add(fCursorWireHex);

            // Step back via prev_block.
            var prevWireHex = Hex(BlockHeaderHasher.PrevBlock(fCursor));
            if (!chain.TryGetByWireHashHex(prevWireHex, out var prevHeader, out var prevHeight))
            {
                // Exited the retained window without meeting the active
                // chain → degraded.
                return BuildDegradedPlan(chain, forkTipWireHex, forkTipHeight);
            }
            fCursor = prevHeader;
            fCursorWireHex = prevWireHex;
            fCursorHeight = prevHeight;
            stepsWalked++;

            // Soft cap: in pathological cases (e.g. malformed chain with
            // duplicate prev_block pointers forming a loop) the walk
            // could be infinite. The retained window is the natural
            // bound — but add one to allow walking *to* the boundary.
            if (stepsWalked > chain.RetainedHeaderCount + 1)
                return BuildDegradedPlan(chain, forkTipWireHex, forkTipHeight);
        }
    }

    private static ReorgPlan BuildDegradedPlan(HeadersChain chain, string forkTipWireHex, long forkTipHeight) =>
        new(
            CommonAncestorHash: string.Empty,
            CommonAncestorHeight: chain.TipHeight - chain.RetainedHeaderCount,
            OrphanedHashes: Array.Empty<string>(),
            NewChainHashes: Array.Empty<string>(),
            NewTipHash: WireHexToDisplayHex(forkTipWireHex),
            NewTipHeight: forkTipHeight,
            IsDegraded: true);

    private static string Hex(ReadOnlySpan<byte> b) =>
        Convert.ToHexString(b).ToLowerInvariant();

    private static string WireHexToDisplayHex(string wireHex)
    {
        if (wireHex.Length != 64) return wireHex; // defensive — should never happen
        Span<char> reversed = stackalloc char[64];
        for (var i = 0; i < 32; i++)
        {
            reversed[i * 2] = wireHex[(31 - i) * 2];
            reversed[i * 2 + 1] = wireHex[(31 - i) * 2 + 1];
        }
        return new string(reversed);
    }
}
