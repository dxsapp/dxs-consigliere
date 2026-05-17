#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Thin interface over <see cref="TxRelayCoordinator.AnnounceAsync"/>
/// to keep the W3 orphan-tx re-broadcaster testable without standing
/// up the full Gate-3 relay graph. <see cref="TxRelayCoordinator"/>
/// implements this; tests inject a fake.
/// </summary>
public interface ITxAnnouncer
{
    /// <summary>Returns the number of Ready peers the inv was sent to.</summary>
    Task<int> AnnounceAsync(string txId, string rawHex, CancellationToken cancellationToken);
}
