#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 S4 — thin abstraction so the orphan-tx re-broadcaster can
/// be unit-tested without standing up a Raven document store. The
/// default Raven-backed implementation
/// <see cref="OutgoingTransactionStoreRawLookup"/> delegates to
/// <see cref="Data.P2p.OutgoingTransactionStore.GetOrNullAsync"/>.
/// </summary>
public interface IOutgoingRawLookup
{
    Task<string?> GetRawHexAsync(string txId, CancellationToken cancellationToken);
}
