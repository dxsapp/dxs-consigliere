using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Session;
using Dxs.Infrastructure.Common;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Catalog descriptor + health for the in-house BSV P2P thin node as a
/// routable provider (<c>p2p</c>). Capabilities: realtime ingest (the
/// always-on mempool observer) and rawTx fetch (<c>getdata</c>). Health
/// is the live ready-peer count from <see cref="BsvP2pHealth"/>: no
/// ready peers ⇒ Degraded (the route's external fallbacks carry the
/// load), at least one ⇒ Healthy.
/// </summary>
public sealed class P2pProviderDiagnostics(BsvP2pHealth health) : IExternalChainProviderDiagnostics
{
    public ExternalChainProviderDescriptor Descriptor { get; } = new(
        ExternalChainProviderName.P2p,
        [
            ExternalChainCapability.RealtimeIngest,
            ExternalChainCapability.RawTxFetch
        ]
    );

    public ValueTask<ExternalChainProviderHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var readyPeers = health.ActiveSessions.Count(s => s.State == PeerSessionState.Ready);
        var (state, detail) = !health.Bound
            ? (ExternalChainHealthState.Degraded, "P2P subsystem is not running (Consigliere:Broadcast:P2p:Enabled=false).")
            : readyPeers > 0
                ? (ExternalChainHealthState.Healthy, $"{readyPeers} ready peer(s).")
                : (ExternalChainHealthState.Degraded, "No ready peers; external fallbacks serving until the pool warms up.");

        return ValueTask.FromResult(
            new ExternalChainProviderHealthSnapshot(
                Descriptor.Provider,
                state,
                detail,
                System.DateTimeOffset.UtcNow
            )
        );
    }
}
