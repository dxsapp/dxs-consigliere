using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Consigliere.BackgroundTasks.Blocks;

public interface IJungleBusBlockSyncScheduler
{
    Task ScheduleUpToHeightAsync(int observedHeight, string subscriptionId, CancellationToken cancellationToken);
}
