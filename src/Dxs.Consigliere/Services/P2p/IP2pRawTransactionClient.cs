#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// thin-node-primary-source S2 — on-demand raw-transaction fetch over
/// the P2P pool. When <c>p2p</c> is the resolved rawTx primary,
/// <see cref="Dxs.Consigliere.Services.Impl.RawTransactionFetchService"/>
/// asks this client for the bytes before falling through to the external
/// REST providers.
///
/// <para>
/// Contract: <see cref="TryGetRawAsync"/> returns the raw tx bytes on a
/// hit and <c>null</c> on miss/timeout/no-ready-peers/subsystem-off. It
/// MUST NOT throw on a miss — the fetch service's primary→fallback loop
/// treats a thrown transport error as a hard failure that aborts the
/// remaining providers, so a P2P miss has to look like a clean "not
/// found here, try the next source".
/// </para>
/// </summary>
public interface IP2pRawTransactionClient
{
    /// <summary>
    /// Issue <c>getdata(MSG_TX, txid)</c> to ready peers and await the
    /// first matching <c>tx</c> frame up to a bounded timeout. Returns
    /// the verified raw tx bytes, or <c>null</c> if no peer served the
    /// tx within the timeout (mempool miss / confirmed tx / cold pool).
    /// </summary>
    /// <param name="txId">Canonical display-order txid hex (64 chars).</param>
    Task<byte[]?> TryGetRawAsync(string txId, CancellationToken cancellationToken = default);
}
