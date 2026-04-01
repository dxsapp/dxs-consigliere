using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Consigliere.BackgroundTasks.Blocks;

public interface IJungleBusBlockSyncScheduler
{
    Task<JungleBusBlockSyncScheduleResult> ScheduleUpToHeightAsync(int observedHeight, string subscriptionId, CancellationToken cancellationToken);
}

public sealed record JungleBusBlockSyncScheduleResult(
    bool Scheduled,
    int ObservedHeight,
    int? FromHeight,
    int? ToHeight);
