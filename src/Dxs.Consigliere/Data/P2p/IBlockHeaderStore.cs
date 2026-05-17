#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.P2p;

namespace Dxs.Consigliere.Data.P2p;

/// <summary>
/// Storage surface for <see cref="BlockHeaderDocument"/>. Production
/// binding is <see cref="BlockHeaderStore"/> (Raven). Tests / spikes
/// may substitute an in-memory implementation — this keeps the
/// HeadersChainService consumable without an embedded Raven runtime
/// (added per audit A2 H2).
/// </summary>
public interface IBlockHeaderStore
{
    Task SaveAsync(BlockHeaderDocument doc, CancellationToken ct = default);
    Task<BlockHeaderDocument> GetByHashAsync(string hashHex, CancellationToken ct = default);
    Task<BlockHeaderDocument> GetTipAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BlockHeaderDocument>> RecentAsync(int count, CancellationToken ct = default);
    Task PruneBelowAsync(long minHeight, CancellationToken ct = default);

    /// <summary>
    /// Audit W3 A2-followup N1 — persistent active-tip pointer. Wave 1
    /// (and the pre-fix W3) decided the active tip on startup via
    /// <see cref="GetTipAsync"/> = <c>ORDER BY Height DESC</c>; that
    /// was unsafe in two ways:
    /// 1. fork headers are persisted at the moment they arrive (with
    ///    <c>promoteToTip = false</c> in-memory); after restart, a
    ///    taller fork that the in-memory work-comparator REJECTED
    ///    would still win by height;
    /// 2. equal-height competing tips have no deterministic winner.
    /// W3 now writes an explicit active-tip pointer document on every
    /// promotion (initial extend OR reorg) and reads it on startup;
    /// fork persistence does NOT touch the pointer.
    /// </summary>
    Task SetActiveTipAsync(string blockHashHex, long height, CancellationToken ct = default);

    /// <summary>
    /// Audit W3 A2-followup N1 — returns the persisted active-tip
    /// pointer, or <c>null</c> on first cold start (no header
    /// extended yet).
    /// </summary>
    Task<BlockHeaderActiveTip?> GetActiveTipAsync(CancellationToken ct = default);
}

/// <summary>
/// W3 A2-followup N1 — the persistent active-tip pointer payload.
/// Stored as a single-row document under a constant ID
/// (<c>block-headers/active-tip</c>); read on startup before any
/// height-based query so a rejected fork cannot become the active
/// chain after restart.
/// </summary>
public sealed record BlockHeaderActiveTip(string BlockHashHex, long Height);
