using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Data.Transactions;

public sealed class TxLifecycleProjectionReader(IDocumentStore documentStore)
{
    public async Task<TxLifecycleProjectionDocument> LoadAsync(
        string txId,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        return await session.LoadAsync<TxLifecycleProjectionDocument>(
            TxLifecycleProjectionDocument.GetId(txId),
            cancellationToken
        );
    }
}
