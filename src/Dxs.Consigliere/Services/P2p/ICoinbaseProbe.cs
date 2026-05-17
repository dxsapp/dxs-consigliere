#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 A2 H2 fix — explicit coinbase classification for the
/// re-broadcaster. Coinbase transactions are block-bound by consensus
/// and MUST NOT be re-announced after a reorg (the receiving peer
/// will always reject them); they need an explicit skip path with its
/// own counter rather than being accidentally filtered by "no raw
/// bytes recorded".
///
/// <para>Default implementation
/// <see cref="MetaTransactionCoinbaseProbe"/> looks up
/// <see cref="Data.Models.Transactions.MetaTransaction"/> by txid and
/// checks <c>Index == 0</c>. If no MetaTransaction exists for the
/// txid the probe returns <c>false</c> (treat as non-coinbase, fall
/// through to the rebroadcaster's raw-lookup chain).</para>
/// </summary>
public interface ICoinbaseProbe
{
    Task<bool> IsCoinbaseAsync(string txId, CancellationToken cancellationToken);
}
