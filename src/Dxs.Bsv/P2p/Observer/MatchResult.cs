#nullable enable
using System.Collections.Generic;

namespace Dxs.Bsv.P2p.Observer;

/// <summary>
/// Outcome of <see cref="WatchlistMatcher.Match"/>. Discriminated
/// record so the consumer (W2 S4 mempool watcher) can branch on
/// hit / miss without null-checking lists.
/// </summary>
public abstract record MatchResult
{
    public sealed record None : MatchResult
    {
        public static readonly None Instance = new();
        private None() { }
    }

    public sealed record AddressHit(IReadOnlyList<byte[]> Hash160s) : MatchResult;

    public sealed record TokenHit(IReadOnlyList<string> TokenIds) : MatchResult;

    public sealed record Both(
        IReadOnlyList<byte[]> Hash160s,
        IReadOnlyList<string> TokenIds) : MatchResult;
}
