using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Cache;
using Dxs.Common.Cache;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Data.Transactions;

public sealed class TxLifecycleProjectionReader(
    IDocumentStore documentStore,
    IProjectionReadCache projectionReadCache,
    IProjectionReadCacheKeyFactory cacheKeyFactory
)
{
    public TxLifecycleProjectionReader(IDocumentStore documentStore)
        : this(documentStore, new NoopProjectionReadCache(), new ProjectionReadCacheKeyFactory())
    {
    }

    public async Task<TxLifecycleProjectionDocument> LoadAsync(
        string txId,
        CancellationToken cancellationToken = default
    )
    {
        var descriptor = cacheKeyFactory.CreateTxLifecycle(txId);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            new ProjectionCacheEntryOptions { Tags = descriptor.Tags },
            async ct =>
            {
                using var session = documentStore.GetNoCacheNoTrackingSession();
                return await session.LoadAsync<TxLifecycleProjectionDocument>(
                    TxLifecycleProjectionDocument.GetId(txId),
                    ct
                );
            },
            cancellationToken);
    }
}
