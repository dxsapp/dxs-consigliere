#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 S3 — enumerates the txids whose
/// <see cref="Dxs.Consigliere.Data.Transactions.TxLifecycleProjectionDocument"/>
/// currently records <c>BlockHash = orphanedBlockHash</c>. Used by the
/// reorg emitter to collect orphaned-block tx lists from our own
/// already-validated projection state (the W3 design pivot away from
/// fetching full block bodies over P2P — BSV mainnet blocks are GB-scale,
/// and the projection's BlockHash index already gives us the same
/// information we'd need).
/// </summary>
public interface IOrphanedTxIdReader
{
    Task<IReadOnlyList<string>> GetTxIdsByBlockHashAsync(
        string blockHashDisplayHex,
        CancellationToken cancellationToken);
}
