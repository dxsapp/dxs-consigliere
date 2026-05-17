#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 — thin interface over
/// <see cref="Dxs.Consigliere.Data.Transactions.TxLifecycleProjectionRebuilder"/>'s
/// public entry point. Lets <see cref="ReorgPipeline"/> drive the
/// rebuilder synchronously after journal-appending Disconnected events
/// so the subsequent <c>OnReorg</c> hub emit reflects an already-
/// reconciled projection state (Core Rule §9 post-audit-fix).
/// </summary>
public interface IProjectionRebuilder
{
    Task RebuildAsync(CancellationToken cancellationToken);
}
