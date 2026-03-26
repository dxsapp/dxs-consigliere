using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Tracking.HistoryBackfill;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Services.Impl;

public sealed class HistoricalTokenBackfillRunner(
    IDocumentStore documentStore,
    ITrackedEntityLifecycleOrchestrator lifecycleOrchestrator
)
{
    public async Task<bool> RunNextAsync(CancellationToken cancellationToken = default)
    {
        TokenHistoryBackfillJobDocument job;
        using (var session = documentStore.GetSession())
        {
            job = await session.Query<TokenHistoryBackfillJobDocument>()
                .Where(x => x.Status == HistoryBackfillExecutionStatus.Queued
                    || x.Status == HistoryBackfillExecutionStatus.Running
                    || x.Status == HistoryBackfillExecutionStatus.WaitingRetry)
                .OrderBy(x => x.RequestedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (job is null)
            return false;

        using var updateSession = documentStore.GetSession();
        var liveJob = await updateSession.LoadAsync<TokenHistoryBackfillJobDocument>(job.Id, cancellationToken);
        if (liveJob is not null)
        {
            liveJob.Status = HistoryBackfillExecutionStatus.Failed;
            liveJob.ErrorCode = "historical_token_scan_not_implemented";
            liveJob.LastProgressAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            liveJob.SetUpdate();
            await updateSession.SaveChangesAsync(cancellationToken);
        }

        await lifecycleOrchestrator.MarkTokenHistoryBackfillFailedAsync(job.TokenId, "historical_token_scan_not_implemented", cancellationToken);
        return true;
    }
}
