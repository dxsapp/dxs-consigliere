using Dxs.Consigliere.Data.Models.Runtime;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Data.Runtime;

public sealed class OperatorRuntimeSettingsStore(IDocumentStore documentStore) : IOperatorRuntimeSettingsStore
{
    public async Task<OperatorRuntimeSettingsDocument> GetAsync(CancellationToken cancellationToken = default)
    {
        using var session = documentStore.OpenAsyncSession();
        return await session.LoadAsync<OperatorRuntimeSettingsDocument>(
            OperatorRuntimeSettingsDocument.DocumentId, cancellationToken);
    }

    public async Task<OperatorRuntimeSettingsDocument> SaveAsync(
        OperatorRuntimeSettingsDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var session = documentStore.OpenAsyncSession();
        document.Id = OperatorRuntimeSettingsDocument.DocumentId;
        document.SetUpdate();

        await session.StoreAsync(document, document.Id, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        return document;
    }
}
