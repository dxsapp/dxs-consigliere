using Dxs.Consigliere.Data.Models.Runtime;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Data.Runtime;

public sealed class SetupBootstrapStore(IDocumentStore documentStore) : ISetupBootstrapStore
{
    public SetupBootstrapDocument Get()
    {
        using var session = documentStore.OpenSession();
        return session.Load<SetupBootstrapDocument>(SetupBootstrapDocument.DocumentId);
    }

    public async Task<SetupBootstrapDocument> SaveAsync(SetupBootstrapDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var session = documentStore.OpenAsyncSession();
        document.Id = SetupBootstrapDocument.DocumentId;
        document.SetUpdate();

        await session.StoreAsync(document, document.Id, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        return document;
    }
}
