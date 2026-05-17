#nullable enable
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.P2p;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 S4 — production-default <see cref="IOutgoingRawLookup"/>
/// backed by <see cref="OutgoingTransactionStore"/>.
/// </summary>
public sealed class OutgoingTransactionStoreRawLookup : IOutgoingRawLookup
{
    private readonly OutgoingTransactionStore _store;

    public OutgoingTransactionStoreRawLookup(OutgoingTransactionStore store)
    {
        _store = store;
    }

    public async Task<string?> GetRawHexAsync(string txId, CancellationToken cancellationToken)
    {
        var outgoing = await _store.GetOrNullAsync(txId, cancellationToken);
        return string.IsNullOrEmpty(outgoing?.RawHex) ? null : outgoing!.RawHex;
    }
}
