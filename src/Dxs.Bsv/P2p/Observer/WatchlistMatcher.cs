#nullable enable
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Dxs.Bsv.P2p.Observer;

/// <summary>
/// Wave 2 S2 — in-memory watchlist matcher answering "does this tx
/// touch any watched address or token?" with O(1) hot path.
///
/// <para>
/// Address matching uses a two-stage index per program Core Rule §3:
/// a <see cref="HashSet{T}"/> over the first 8 bytes of
/// <c>Hash160</c> interpreted little-endian as <see cref="ulong"/>,
/// plus a full-hash dictionary keyed by the same prefix. Prefix hits
/// are then verified against the full 20-byte hash so a (rare)
/// 8-byte prefix collision can't escape as a false positive.
/// </para>
/// <para>
/// Token matching is direct hash-set membership on the token id
/// string (typically a 64-char lowercase hex). Token watchlists are
/// small relative to addresses; no prefix index needed.
/// </para>
/// <para>
/// All add/remove operations are concurrent-safe (use
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> under the hood).
/// The matcher's <see cref="Match"/> method is allocation-free in
/// the no-hit case and only allocates the result lists on a hit.
/// </para>
/// </summary>
public sealed class WatchlistMatcher
{
    private const int Hash160Size = TxScriptParser.Hash160Size;

    // 8-byte prefix → set of full 20-byte hashes. Full set per prefix
    // handles the (rare) 8-byte prefix collision case: two distinct
    // hash160s sharing the prefix can both be watched, or neither, or
    // one — the full-hash verify resolves which.
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<Hash160Key, byte>> _addressByPrefix =
        new();

    private readonly ConcurrentDictionary<string, byte> _tokenIds = new(StringComparer.Ordinal);

    /// <summary>True once <see cref="MarkLoaded"/> has been called.
    /// Consumers wait on this before processing observed tx, so the
    /// matcher is warm with the initial Raven snapshot.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>True when at least one <see cref="WatchingToken"/>
    /// is registered. Mempool watcher (S4) widens parsing scope to
    /// every tx when this is true (per program Core Rule §3 token
    /// matching note).</summary>
    public bool HasAnyTokens => !_tokenIds.IsEmpty;

    public int WatchedAddressCount
    {
        get
        {
            var n = 0;
            foreach (var bucket in _addressByPrefix.Values) n += bucket.Count;
            return n;
        }
    }

    public int WatchedTokenCount => _tokenIds.Count;

    /// <summary>
    /// Mark the matcher as loaded — called by
    /// <c>RavenWatchlistLoader</c> (S3) after the initial bulk load
    /// completes. Once true it stays true; subsequent Add/Remove
    /// calls are still respected (this flag is independent of
    /// individual deltas).
    /// </summary>
    public void MarkLoaded() => IsLoaded = true;

    /// <summary>Add a watched address by its 20-byte hash160. Idempotent.</summary>
    public void AddAddress(ReadOnlySpan<byte> hash160)
    {
        if (hash160.Length != Hash160Size)
            throw new ArgumentException($"hash160 must be {Hash160Size} bytes", nameof(hash160));
        var prefix = ReadPrefix(hash160);
        var bucket = _addressByPrefix.GetOrAdd(prefix, _ => new ConcurrentDictionary<Hash160Key, byte>());
        bucket.TryAdd(new Hash160Key(hash160.ToArray()), 0);
    }

    /// <summary>Remove a watched address by its 20-byte hash160.
    /// Idempotent — no-op if not present.</summary>
    public void RemoveAddress(ReadOnlySpan<byte> hash160)
    {
        if (hash160.Length != Hash160Size) return;
        var prefix = ReadPrefix(hash160);
        if (!_addressByPrefix.TryGetValue(prefix, out var bucket)) return;
        bucket.TryRemove(new Hash160Key(hash160.ToArray()), out _);
        if (bucket.IsEmpty)
            _addressByPrefix.TryRemove(prefix, out _);
    }

    /// <summary>Add a watched token id. Idempotent.</summary>
    public void AddToken(string tokenId)
    {
        if (!string.IsNullOrEmpty(tokenId))
            _tokenIds.TryAdd(tokenId, 0);
    }

    /// <summary>Remove a watched token id. Idempotent.</summary>
    public void RemoveToken(string tokenId)
    {
        if (!string.IsNullOrEmpty(tokenId))
            _tokenIds.TryRemove(tokenId, out _);
    }

    /// <summary>
    /// Decide whether <paramref name="parsed"/> touches any watched
    /// address (output P2PKH or input P2PKH payer) or any watched
    /// token. Returns <see cref="MatchResult.None"/> when nothing
    /// matched — no allocations on that path.
    /// </summary>
    public MatchResult Match(ParsedTx parsed)
    {
        if (parsed is null) return MatchResult.None.Instance;

        List<byte[]>? addressHits = null;
        List<string>? tokenHits = null;

        // Address matches against P2PKH outputs.
        for (var i = 0; i < parsed.OutputHash160s.Count; i++)
        {
            var h = parsed.OutputHash160s[i];
            if (IsWatchedAddress(h)) (addressHits ??= new List<byte[]>()).Add(h);
        }
        // Address matches against P2PKH input payer hashes.
        for (var i = 0; i < parsed.InputPayerHash160s.Count; i++)
        {
            var h = parsed.InputPayerHash160s[i];
            if (IsWatchedAddress(h)) (addressHits ??= new List<byte[]>()).Add(h);
        }
        // Token matches against output token ids.
        for (var i = 0; i < parsed.OutputTokenIds.Count; i++)
        {
            var t = parsed.OutputTokenIds[i];
            if (_tokenIds.ContainsKey(t)) (tokenHits ??= new List<string>()).Add(t);
        }

        return (addressHits, tokenHits) switch
        {
            (null, null) => MatchResult.None.Instance,
            (not null, null) => new MatchResult.AddressHit(addressHits),
            (null, not null) => new MatchResult.TokenHit(tokenHits),
            (not null, not null) => new MatchResult.Both(addressHits, tokenHits),
        };
    }

    /// <summary>
    /// True if <paramref name="hash160"/> is in the watchlist. Used
    /// internally by <see cref="Match"/>; exposed public so the
    /// mempool watcher can short-circuit early when needed.
    /// </summary>
    public bool IsWatchedAddress(byte[] hash160)
    {
        if (hash160 is null || hash160.Length != Hash160Size) return false;
        var prefix = ReadPrefix(hash160);
        if (!_addressByPrefix.TryGetValue(prefix, out var bucket)) return false;
        return bucket.ContainsKey(new Hash160Key(hash160));
    }

    /// <summary>True if the token id is watched.</summary>
    public bool IsWatchedToken(string tokenId) =>
        !string.IsNullOrEmpty(tokenId) && _tokenIds.ContainsKey(tokenId);

    private static ulong ReadPrefix(ReadOnlySpan<byte> hash160) =>
        BinaryPrimitives.ReadUInt64LittleEndian(hash160[..8]);

    /// <summary>
    /// Byte-array key wrapper with structural equality. The .NET
    /// <see cref="byte[]"/> default equality is reference-based, so
    /// using it directly as a dictionary key would never match
    /// different array instances even with the same contents.
    /// </summary>
    private readonly struct Hash160Key : IEquatable<Hash160Key>
    {
        public readonly byte[] Bytes;
        public Hash160Key(byte[] bytes) => Bytes = bytes;

        public bool Equals(Hash160Key other) =>
            Bytes.AsSpan().SequenceEqual(other.Bytes);

        public override bool Equals(object? obj) => obj is Hash160Key k && Equals(k);

        public override int GetHashCode()
        {
            // Reuse the 8-byte LE prefix as the hash code — it's
            // already what the outer dictionary keys on, so this
            // gives perfect prefix-bucket distribution.
            if (Bytes is null || Bytes.Length < 8) return 0;
            return (int)BinaryPrimitives.ReadUInt64LittleEndian(Bytes.AsSpan(0, 8));
        }
    }
}
