#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 S4 — re-announces orphaned-block transactions back into the
/// network mempool after a reorg. Looks up raw bytes via
/// <see cref="Data.P2p.OutgoingTransactionStore"/> first, falls back to
/// <see cref="Data.IRawTransactionPayloadStore"/>, skips txs with no
/// raw bytes recorded anywhere. Uses
/// <see cref="TxRelayCoordinator.AnnounceAsync"/> as the single announce
/// primitive (same path Gate-3 outbound broadcast uses).
/// </summary>
public interface IOrphanedTxRebroadcaster
{
    /// <summary>
    /// Per-orphan-block tx list. Outer key = orphan block hash
    /// (display-hex); value = the txids the projection records as
    /// having confirmed in that block. The implementation walks each
    /// list, attempts a re-announce per txid, and updates counters on
    /// the injected <see cref="OrphanedTxRebroadcastRecorder"/>.
    /// </summary>
    Task RebroadcastAsync(
        IReadOnlyDictionary<string, IReadOnlyList<string>> orphanedTxIdsPerBlock,
        CancellationToken cancellationToken);
}
