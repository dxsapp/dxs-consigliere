#nullable enable
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Transactions;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 — adapter that lets <see cref="ReorgPipeline"/> drive the
/// sealed <see cref="TxLifecycleProjectionRebuilder"/> via the
/// <see cref="IProjectionRebuilder"/> abstraction (which keeps the
/// pipeline unit-testable with a fake rebuilder).
/// </summary>
public sealed class TxLifecycleProjectionRebuilderAdapter : IProjectionRebuilder
{
    private readonly TxLifecycleProjectionRebuilder _inner;

    public TxLifecycleProjectionRebuilderAdapter(TxLifecycleProjectionRebuilder inner)
    {
        _inner = inner;
    }

    public async Task RebuildAsync(CancellationToken cancellationToken)
    {
        await _inner.RebuildAsync(cancellationToken: cancellationToken);
    }
}
