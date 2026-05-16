using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Pool;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Singleton surface that exposes live P2P pool state for diagnostics /
/// admin endpoints / soak metrics. Bound by <see cref="BsvP2pHostedService"/>
/// at startup; safe to read at any time (returns inert values when unbound).
/// </summary>
public sealed class BsvP2pHealth
{
    private PeerManager? _manager;
    private InMemoryPeerStore? _store;

    public void Bind(PeerManager manager, InMemoryPeerStore store)
    {
        _manager = manager;
        _store = store;
    }

    public void Unbind()
    {
        _manager = null;
        _store = null;
    }

    public bool Bound => _manager is not null;

    public int PoolSize => _manager?.PoolSize ?? 0;
    public int TargetPoolSize => _manager?.TargetPoolSize ?? 0;
    public int Subnet24Diversity => _manager?.Subnet24Diversity ?? 0;

    public IReadOnlyCollection<string> ActivePeerKeys =>
        _manager is null
            ? new List<string>()
            : _manager.ActiveSessions.Keys.OrderBy(k => k).ToList();

    public async Task<IReadOnlyList<PeerRecord>> ListAllAsync(CancellationToken ct)
    {
        if (_store is null) return new List<PeerRecord>();
        return await _store.ListAllAsync(ct);
    }
}
