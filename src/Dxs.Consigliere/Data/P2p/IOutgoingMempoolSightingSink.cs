using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Consigliere.Data.P2p;

/// <summary>
/// Narrow seam the P2P mempool observer uses to tell the outgoing-tx
/// lifecycle that one of OUR broadcasts has been seen back in a peer's
/// mempool (the relay-back the observer just fetched + ingested). This is
/// the FIRST independent confirmation a thin node gets that its tx
/// propagated, so it advances PeerAcked/PeerRelayed → MempoolSeen without
/// waiting for the relay coordinator's ≥2 relay-back count.
///
/// Implemented by <c>OutgoingTransactionMonitor</c>; it no-ops for any txid
/// that isn't a tracked outgoing transaction, so the observer can call it
/// for every matched ingest safely.
/// </summary>
public interface IOutgoingMempoolSightingSink
{
    Task OnMempoolSightingAsync(string txId, CancellationToken ct = default);
}
