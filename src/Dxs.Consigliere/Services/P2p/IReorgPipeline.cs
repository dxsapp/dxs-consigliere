#nullable enable
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Messages;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 S3 — wave-level entrypoint invoked by
/// <see cref="HeadersChainService"/> on every observed
/// <see cref="Dxs.Bsv.P2p.Chain.ExtendResult.Fork"/> outcome. Runs the
/// reorg detection + journal emission + hub event + orphaned-tx
/// re-broadcast pipeline.
/// </summary>
public interface IReorgPipeline
{
    /// <summary>
    /// Process a freshly-stored fork-side tip. Idempotent: if the fork
    /// does not actually promote to a new active chain (no plan
    /// returned by the detector, or every journal append returns
    /// <c>IsDuplicate = true</c>), this is a no-op aside from a single
    /// trace log.
    /// </summary>
    Task HandleForkObservedAsync(
        BlockHeader forkTip,
        CancellationToken cancellationToken);
}
