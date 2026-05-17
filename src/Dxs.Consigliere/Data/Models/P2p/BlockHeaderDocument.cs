using System;

namespace Dxs.Consigliere.Data.Models.P2p;

/// <summary>
/// One persisted BSV block header. Stored in Raven collection
/// "BlockHeaders". Document id is `block-headers/&lt;wire-order-hash-hex&gt;`
/// so primary-key lookup by hash is a direct LoadAsync.
///
/// Hash format here is wire-order (little-endian) lowercase hex, the
/// same convention used by <see cref="Dxs.Bsv.P2p.Chain.BlockHeaderHasher"/>.
/// Display order (the strings on block explorers) is the byte-reverse
/// of this and is left to the admin API to surface if needed.
///
/// Schema frozen for Wave 1 consumption by W2/W3 (tx-confirm checks,
/// reorg ancestor walks). Field additions are non-breaking; renames or
/// type changes require a wave-level decision.
/// </summary>
public sealed class BlockHeaderDocument
{
    public string Id { get; set; }

    /// <summary>Wire-order hash hex (lowercase, no 0x prefix).</summary>
    public string Hash { get; set; }

    /// <summary>Chain height for this header.</summary>
    public long Height { get; set; }

    /// <summary>Wire-order prev_block hash hex (lowercase, no 0x prefix).</summary>
    public string PrevHash { get; set; }

    /// <summary>Block timestamp in unix milliseconds (extracted from header).</summary>
    public long TimestampMs { get; set; }

    /// <summary>Full 80-byte raw header. PoW remains verifiable from this.</summary>
    public byte[] HeaderBytes80 { get; set; }

    /// <summary>UTC milliseconds when we observed and persisted this header.</summary>
    public long ObservedAtMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static string BuildId(string hashHex) => $"block-headers/{hashHex}";
}
