using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Infrastructure.JungleBus.Dto;

namespace Dxs.Consigliere.Data.Runtime;

public interface IJungleBusBlockSyncHealthStore
{
    Task<JungleBusBlockSyncHealthDocument> GetAsync(CancellationToken cancellationToken = default);
    Task TouchControlMessageAsync(string subscriptionId, PubControlMessageDto message, CancellationToken cancellationToken = default);
    Task TouchScheduledAsync(string subscriptionId, JungleBusBlockSyncScheduleResult result, CancellationToken cancellationToken = default);
    Task TouchProcessedAsync(string requestId, int? processedBlockHeight, CancellationToken cancellationToken = default);
    Task RecordErrorAsync(string error, CancellationToken cancellationToken = default);
}
