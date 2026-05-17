#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using Dxs.Bsv.P2p.Messages;

namespace Dxs.Bsv.P2p.Chain;

/// <summary>
/// Outcome of feeding one <see cref="BlockHeader"/> to
/// <see cref="HeadersChain.TryExtend"/>. Discriminated record to keep
/// downstream code branchful and total.
/// </summary>
public abstract record ExtendResult
{
    /// <summary>Header was a valid extension of the current tip. Stored.</summary>
    public sealed record Extended(BlockHeader Header, long Height) : ExtendResult;

    /// <summary>Header equals the current tip or matches an already-retained ancestor.</summary>
    public sealed record AlreadyKnown(BlockHeader Header, long Height) : ExtendResult;

    /// <summary>
    /// Header's <c>prev_block</c> matches a non-tip ancestor still in the
    /// retained window. Stored as a competing tip candidate but no
    /// rebuild is attempted in W1 — Wave 3 owns reorg recovery.
    /// </summary>
    public sealed record Fork(BlockHeader Header, long ParentHeight) : ExtendResult;

    /// <summary>
    /// Header's <c>prev_block</c> is unknown (either below the retained
    /// window or in an unrelated chain). Caller may request more headers
    /// from a peer via <c>getheaders</c>.
    /// </summary>
    public sealed record Orphan(BlockHeader Header, byte[] MissingParentHash) : ExtendResult;

    /// <summary>Header failed structural validation (size, PoW).</summary>
    public sealed record Invalid(string Reason) : ExtendResult;

    /// <summary>
    /// Chain is empty and no height-aware seed has been applied via
    /// <see cref="HeadersChain.Seed"/>. We refuse to auto-assign height
    /// 0 to the first arbitrary P2P header — that would yield a useless
    /// "tip" miles off the real mainnet height. Caller (the hosted
    /// service) should log a warning and wait for the bootstrapper or
    /// operator to anchor the chain. Added per audit A2 H1.
    /// </summary>
    public sealed record Unanchored(BlockHeader Header) : ExtendResult;
}

/// <summary>
/// In-memory header-chain state. Owns no I/O — the hosted service in S3
/// loads from Raven via <see cref="LoadFromStore"/>, feeds incoming
/// headers through <see cref="TryExtend"/>, and persists successful
/// extensions.
///
/// The chain tracks at most <c>RetainedHeaderCount</c> headers anchored
/// on a current tip. Forks are recorded (so callers can persist them)
/// but no rebuild / ancestor walking is performed in W1.
///
/// Wave 1 explicitly does NOT implement reorg recovery — see
/// docs/stream-tasks/bsv-headers-chain-wave/master.md §Scope.
/// </summary>
public sealed class HeadersChain
{
    private readonly HeadersChainOptions _options;

    // Hash hex (lowercase) → (header, height). Linked-list semantics aren't
    // needed — height is the canonical ordering and prev_block links via hash.
    private readonly Dictionary<string, (BlockHeader Header, long Height)> _byHash =
        new(StringComparer.Ordinal);

    private BlockHeader? _tip;
    private long _tipHeight;
    private bool _loaded;

    public HeadersChain(HeadersChainOptions? options = null)
    {
        _options = options ?? new HeadersChainOptions();
    }

    /// <summary>Current chain tip header, or null if the chain is empty.</summary>
    public BlockHeader? Tip => _tip;

    /// <summary>Height of the current tip; 0 when chain is empty.</summary>
    public long TipHeight => _tipHeight;

    /// <summary>Number of headers currently retained in memory.</summary>
    public int Count => _byHash.Count;

    /// <summary>
    /// Load a previously-persisted set of headers (e.g. the trailing
    /// <c>RetainedHeaderCount</c> from Raven). Highest-height entry
    /// becomes the tip; ancestors below are retained for fork detection.
    /// Does not re-validate PoW (we trust the persisted chain up to its
    /// current tip — per Core Rule §6 in the wave master.md).
    /// </summary>
    public void LoadFromStore(IReadOnlyList<(BlockHeader Header, long Height)> persisted)
    {
        if (persisted is null) throw new ArgumentNullException(nameof(persisted));
        _byHash.Clear();
        _tip = null;
        _tipHeight = 0;

        foreach (var (header, height) in persisted)
        {
            var key = HashKey(header);
            _byHash[key] = (header, height);
            if (_tip is null || height > _tipHeight)
            {
                _tip = header;
                _tipHeight = height;
            }
        }
        _loaded = true;
    }

    /// <summary>
    /// Feed a single header into the chain. Returns a discriminated
    /// <see cref="ExtendResult"/> describing what happened. Never returns
    /// null. See result subtypes for semantics. Caller is responsible for
    /// persisting the header in the Extended / Fork cases.
    /// </summary>
    public ExtendResult TryExtend(BlockHeader header)
    {
        if (header is null) throw new ArgumentNullException(nameof(header));
        if (header.Bytes80.Length != BlockHeader.Size)
            return new ExtendResult.Invalid($"Header is {header.Bytes80.Length} bytes, expected {BlockHeader.Size}");

        if (!BlockHeaderHasher.MeetsTarget(header))
            return new ExtendResult.Invalid("PoW target not met");

        var hash = BlockHeaderHasher.Hash(header);
        var hashKey = HexLower(hash);

        // 1) Duplicate of any retained header (including tip).
        if (_byHash.TryGetValue(hashKey, out var existing))
            return new ExtendResult.AlreadyKnown(existing.Header, existing.Height);

        var prevHash = BlockHeaderHasher.PrevBlock(header);
        var prevKey = HexLower(prevHash);

        // 2) Empty chain and no height anchor → Unanchored (audit A2 H1).
        // Auto-assigning height 0 on cold start would produce a useless
        // "tip" disconnected from real mainnet height. Caller must seed
        // via Seed(...) (bootstrapper) or LoadFromStore (restart).
        if (_tip is null)
        {
            return new ExtendResult.Unanchored(header);
        }

        // 3) Extends current tip.
        var tipKey = HexLower(BlockHeaderHasher.Hash(_tip));
        if (string.Equals(prevKey, tipKey, StringComparison.Ordinal))
        {
            var newHeight = _tipHeight + 1;
            AddHeader(header, hashKey, newHeight);
            return new ExtendResult.Extended(header, newHeight);
        }

        // 4) Extends a non-tip ancestor that's still in the retained window.
        if (_byHash.TryGetValue(prevKey, out var parent))
        {
            // Store the fork candidate at parent.Height + 1 but DO NOT
            // promote it to tip (W1 detects forks, W3 recovers from them).
            AddHeader(header, hashKey, parent.Height + 1, promoteToTip: false);
            return new ExtendResult.Fork(header, parent.Height);
        }

        // 5) Parent unknown — orphan.
        return new ExtendResult.Orphan(header, prevHash.ToArray());
    }

    /// <summary>
    /// Returns the most recent <paramref name="count"/> retained headers,
    /// in descending height order (tip first). Useful for admin endpoints
    /// and locator construction for <c>getheaders</c> requests.
    /// </summary>
    public IReadOnlyList<(BlockHeader Header, long Height)> RecentHeaders(int count)
    {
        if (count <= 0) return Array.Empty<(BlockHeader, long)>();
        return _byHash.Values
            .OrderByDescending(x => x.Height)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Returns true if the loader has been called at least once. Even an
    /// empty load counts — distinguishes "fresh cold start" from
    /// "loaded but no headers persisted yet".
    /// </summary>
    public bool IsLoaded => _loaded;

    /// <summary>
    /// Wave 3 S1 — read-only ancestor lookup by wire-order hash hex.
    /// The hash key matches what <see cref="TryExtend"/> stores (the
    /// lowercase hex of <see cref="BlockHeaderHasher.Hash"/>, i.e. wire
    /// order). Used by the reorg detector to walk back from a fork tip
    /// through the retained window. Returns false for unknown hashes.
    /// </summary>
    public bool TryGetByWireHashHex(string wireHashHex, out BlockHeader header, out long height)
    {
        if (_byHash.TryGetValue(wireHashHex, out var entry))
        {
            header = entry.Header;
            height = entry.Height;
            return true;
        }
        header = null!;
        height = 0;
        return false;
    }

    /// <summary>
    /// Wave 3 S1 — the wave-level <c>RetainedHeaderCount</c> option, exposed
    /// read-only so the reorg detector can decide between a "normal" reorg
    /// (fork point inside the window) and a degraded reorg (below window).
    /// </summary>
    public int RetainedHeaderCount => _options.RetainedHeaderCount;

    /// <summary>
    /// Anchor the chain to a specific (header, height) pair. Used by the
    /// bootstrapper (Wave 1 S4) when an external height-aware source
    /// (e.g. WhatsOnChain /chain/info) provides the current tip. After
    /// seeding, subsequent P2P headers can extend the chain normally.
    /// Idempotent on a re-seed of the same header; rejects a re-seed
    /// with a different hash to prevent silent tip overwrite.
    /// Added per audit A2 H1.
    /// </summary>
    public void Seed(BlockHeader header, long height)
    {
        if (header is null) throw new ArgumentNullException(nameof(header));
        if (header.Bytes80.Length != BlockHeader.Size)
            throw new ArgumentException($"header must be {BlockHeader.Size} bytes", nameof(header));
        var key = HashKey(header);
        if (_byHash.TryGetValue(key, out var existing))
        {
            if (existing.Height != height)
                throw new InvalidOperationException($"Cannot re-seed: header already at height {existing.Height}");
            return; // idempotent
        }
        _byHash[key] = (header, height);
        _tip = header;
        _tipHeight = height;
        _loaded = true;
    }

    private void AddHeader(BlockHeader header, string hashKey, long height, bool promoteToTip = true)
    {
        _byHash[hashKey] = (header, height);
        if (promoteToTip && (_tip is null || height > _tipHeight))
        {
            _tip = header;
            _tipHeight = height;
        }
        PruneIfNeeded();
    }

    private void PruneIfNeeded()
    {
        if (_byHash.Count <= _options.RetainedHeaderCount) return;
        var cutoff = _tipHeight - _options.RetainedHeaderCount + 1;
        if (cutoff <= 0) return;
        var stale = _byHash.Where(kv => kv.Value.Height < cutoff).Select(kv => kv.Key).ToList();
        foreach (var key in stale) _byHash.Remove(key);
    }

    private static string HashKey(BlockHeader header) => HexLower(BlockHeaderHasher.Hash(header));

    private static string HexLower(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
