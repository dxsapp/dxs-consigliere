#nullable enable
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.P2p;

namespace Dxs.Consigliere.Data.P2p;

/// <summary>
/// Wave 5 A2 M1 fix — thin abstraction over the subset of
/// <see cref="OutgoingTransactionStore"/> operations the
/// <see cref="Services.IBroadcastService"/> uses. Lets the broadcast
/// service be unit-tested without standing up a real
/// <c>IDocumentStore</c>; production binds the interface to the
/// concrete store via the W2 wirer.
/// </summary>
public interface IOutgoingTransactionRepository
{
    Task SaveAsync(OutgoingTransaction tx, CancellationToken ct = default);
    Task<OutgoingTransaction?> GetOrNullAsync(string txId, CancellationToken ct = default);
}
