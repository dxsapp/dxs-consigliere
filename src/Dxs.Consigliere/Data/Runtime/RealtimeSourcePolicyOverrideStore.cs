using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Data.Runtime;

public sealed class RealtimeSourcePolicyOverrideStore(IDocumentStore documentStore) : IRealtimeSourcePolicyOverrideStore
{
    public async Task<RealtimeSourcePolicyOverrideDocument> GetAsync(CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        return await session.LoadAsync<RealtimeSourcePolicyOverrideDocument>(RealtimeSourcePolicyOverrideDocument.DocumentId, cancellationToken);
    }

    public async Task<RealtimeSourcePolicyOverrideDocument> SaveAsync(
        RealtimeSourcePolicyOverrideDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var session = documentStore.GetSession();
        document.Id = RealtimeSourcePolicyOverrideDocument.DocumentId;
        document.SetUpdate();

        await session.StoreAsync(document, document.Id, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<RealtimeSourcePolicyOverrideDocument> UpsertAsync(
        string primaryRealtimeSource,
        string bitailsTransport,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var document = await session.LoadAsync<RealtimeSourcePolicyOverrideDocument>(RealtimeSourcePolicyOverrideDocument.DocumentId, cancellationToken)
                       ?? new RealtimeSourcePolicyOverrideDocument
                       {
                           Id = RealtimeSourcePolicyOverrideDocument.DocumentId
                       };

        document.PrimaryRealtimeSource = primaryRealtimeSource;
        document.BitailsTransport = bitailsTransport;
        document.UpdatedBy = updatedBy;
        document.SetUpdate();

        await session.StoreAsync(document, document.Id, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var document = await session.LoadAsync<RealtimeSourcePolicyOverrideDocument>(RealtimeSourcePolicyOverrideDocument.DocumentId, cancellationToken);
        if (document is null)
            return;

        session.Delete(document);
        await session.SaveChangesAsync(cancellationToken);
    }
}
