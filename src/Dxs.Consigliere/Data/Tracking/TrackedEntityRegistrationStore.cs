using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Data.Tracking;

public sealed class TrackedEntityRegistrationStore(IDocumentStore documentStore) : ITrackedEntityRegistrationStore
{
    public async Task RegisterAddressAsync(
        string address,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetSession();

        var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address), cancellationToken)
            ?? new TrackedAddressDocument
            {
                Id = TrackedAddressDocument.GetId(address),
                EntityType = TrackedEntityType.Address,
                EntityId = address,
                Address = address,
                Name = name,
            };
        var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(address), cancellationToken)
            ?? new TrackedAddressStatusDocument
            {
                Id = TrackedAddressStatusDocument.GetId(address),
                EntityType = TrackedEntityType.Address,
                EntityId = address,
                Address = address,
            };
        var legacy = await session.Query<WatchingAddress>()
            .FirstOrDefaultAsync(x => x.Address == address, cancellationToken);

        ApplyRegistration(tracked, tracked.Address, tracked.Name, name);
        ApplyRegistration(status, status.Address);

        if (legacy is null)
        {
            legacy = new WatchingAddress
            {
                Id = $"address/{address}",
                Address = address,
                Name = name,
            };

            await session.StoreAsync(legacy, legacy.Id, cancellationToken);
        }

        await StoreIfNewAsync(session, tracked, cancellationToken);
        await StoreIfNewAsync(session, status, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task RegisterTokenAsync(
        string tokenId,
        string symbol,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetSession();

        var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId), cancellationToken)
            ?? new TrackedTokenDocument
            {
                Id = TrackedTokenDocument.GetId(tokenId),
                EntityType = TrackedEntityType.Token,
                EntityId = tokenId,
                TokenId = tokenId,
                Symbol = symbol,
            };
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken)
            ?? new TrackedTokenStatusDocument
            {
                Id = TrackedTokenStatusDocument.GetId(tokenId),
                EntityType = TrackedEntityType.Token,
                EntityId = tokenId,
                TokenId = tokenId,
            };
        var legacy = await session.Query<WatchingToken>()
            .FirstOrDefaultAsync(x => x.TokenId == tokenId, cancellationToken);

        ApplyRegistration(tracked, tracked.TokenId, tracked.Symbol, symbol);
        ApplyRegistration(status, status.TokenId);

        if (legacy is null)
        {
            legacy = new WatchingToken
            {
                Id = $"token/{tokenId}/{symbol}",
                TokenId = tokenId,
                Symbol = symbol,
            };

            await session.StoreAsync(legacy, legacy.Id, cancellationToken);
        }

        await StoreIfNewAsync(session, tracked, cancellationToken);
        await StoreIfNewAsync(session, status, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyRegistration(TrackedAddressDocument document, string address, string currentName, string requestedName)
    {
        document.Address = address;
        document.Name = string.IsNullOrWhiteSpace(requestedName) ? currentName : requestedName;
        ApplyRegistration(document);
    }

    private static void ApplyRegistration(TrackedTokenDocument document, string tokenId, string currentSymbol, string requestedSymbol)
    {
        document.TokenId = tokenId;
        document.Symbol = string.IsNullOrWhiteSpace(requestedSymbol) ? currentSymbol : requestedSymbol;
        ApplyRegistration(document);
    }

    private static void ApplyRegistration(TrackedAddressStatusDocument document, string address)
    {
        document.Address = address;
        ApplyRegistration(document);
    }

    private static void ApplyRegistration(TrackedTokenStatusDocument document, string tokenId)
    {
        document.TokenId = tokenId;
        ApplyRegistration(document);
    }

    private static void ApplyRegistration(TrackedEntityDocumentBase document)
    {
        var wasTombstoned = document.IsTombstoned;

        document.Tracked = true;
        document.IsTombstoned = false;
        document.TombstonedAt = null;

        if (wasTombstoned
            || string.IsNullOrWhiteSpace(document.LifecycleStatus)
            || string.Equals(document.LifecycleStatus, TrackedEntityLifecycleStatus.Paused, StringComparison.Ordinal)
            || string.Equals(document.LifecycleStatus, TrackedEntityLifecycleStatus.Failed, StringComparison.Ordinal))
        {
            document.LifecycleStatus = TrackedEntityLifecycleStatus.Registered;
            document.Readable = false;
            document.Authoritative = false;
            document.Degraded = false;
            document.LagBlocks = null;
            document.Progress = null;
        }

        document.SetUpdate();
    }

    private static async Task StoreIfNewAsync<TDocument>(
        Raven.Client.Documents.Session.IAsyncDocumentSession session,
        TDocument document,
        CancellationToken cancellationToken
    ) where TDocument : Entity
    {
        if (session.Advanced.IsLoaded(document.Id))
            return;

        await session.StoreAsync(document, document.Id, cancellationToken);
    }
}
