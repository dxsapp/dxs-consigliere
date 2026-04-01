using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Extensions;
using Dxs.Infrastructure.JungleBus.Dto;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Data.Runtime;

public sealed class JungleBusBlockSyncHealthStore(IDocumentStore documentStore) : IJungleBusBlockSyncHealthStore
{
    public async Task<JungleBusBlockSyncHealthDocument> GetAsync(CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        return await session.LoadAsync<JungleBusBlockSyncHealthDocument>(JungleBusBlockSyncHealthDocument.DocumentId, cancellationToken);
    }

    public async Task TouchControlMessageAsync(
        string subscriptionId,
        PubControlMessageDto message,
        CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var document = await LoadOrCreateAsync(session, cancellationToken);
        document.SubscriptionId = subscriptionId;
        document.LastControlMessageAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        document.LastControlCode = message.Code;
        document.LastControlStatus = message.Status;
        document.LastControlMessage = message.Message;

        if (message.Block > 0)
        {
            if (!document.LastObservedBlockHeight.HasValue || message.Block > document.LastObservedBlockHeight.Value)
            {
                document.LastObservedMovementAt = document.LastControlMessageAt;
                document.LastObservedMovementHeight = message.Block;
            }

            document.LastObservedBlockHeight = message.Block;
        }
        if (message.TransactionCount > 0)
            document.LastObservedBlockTimestamp = document.LastControlMessageAt;

        document.SetUpdate();
        await session.StoreAsync(document, document.Id, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task TouchScheduledAsync(
        string subscriptionId,
        JungleBusBlockSyncScheduleResult result,
        CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var document = await LoadOrCreateAsync(session, cancellationToken);
        document.SubscriptionId = subscriptionId;

        if (result.ObservedHeight > 0)
            document.LastObservedBlockHeight = result.ObservedHeight;

        if (result.Scheduled)
        {
            document.LastScheduledAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            document.LastScheduledFromHeight = result.FromHeight;
            document.LastScheduledToHeight = result.ToHeight;
        }

        document.SetUpdate();
        await session.StoreAsync(document, document.Id, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task TouchProcessedAsync(
        string requestId,
        int? processedBlockHeight,
        CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var document = await LoadOrCreateAsync(session, cancellationToken);
        document.LastProcessedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        document.LastRequestId = requestId;
        if (processedBlockHeight.HasValue)
        {
            if (!document.LastProcessedBlockHeight.HasValue || processedBlockHeight.Value > document.LastProcessedBlockHeight.Value)
            {
                document.LastLocalProgressAt = document.LastProcessedAt;
                document.LastLocalProgressHeight = processedBlockHeight.Value;
            }

            document.LastProcessedBlockHeight = processedBlockHeight.Value;
        }

        document.SetUpdate();
        await session.StoreAsync(document, document.Id, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordErrorAsync(string error, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var document = await LoadOrCreateAsync(session, cancellationToken);
        document.LastError = error;
        document.LastErrorAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        document.SetUpdate();
        await session.StoreAsync(document, document.Id, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    private static async Task<JungleBusBlockSyncHealthDocument> LoadOrCreateAsync(
        Raven.Client.Documents.Session.IAsyncDocumentSession session,
        CancellationToken cancellationToken)
        => await session.LoadAsync<JungleBusBlockSyncHealthDocument>(JungleBusBlockSyncHealthDocument.DocumentId, cancellationToken)
           ?? new JungleBusBlockSyncHealthDocument
           {
               Id = JungleBusBlockSyncHealthDocument.DocumentId
           };
}
