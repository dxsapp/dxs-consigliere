using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Data.Transactions;

public sealed class TokenValidationDependencyStore(IDocumentStore documentStore) : ITokenValidationDependencyStore
{
    public async Task UpsertAsync(
        TokenValidationDependencySnapshot snapshot,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var session = documentStore.GetSession();
        var existing = await session.LoadAsync<TokenValidationDependencyDocument>(
            TokenValidationDependencyDocument.GetId(snapshot.TxId),
            cancellationToken
        );

        var previousDependencies = existing?.DependsOnTxIds ?? [];
        var nextDependencies = snapshot.DependsOnTxIds.ToArray();
        var removedDependencies = previousDependencies.Except(nextDependencies, StringComparer.Ordinal).ToArray();
        var addedDependencies = nextDependencies.Except(previousDependencies, StringComparer.Ordinal).ToArray();

        var document = existing ?? new TokenValidationDependencyDocument
        {
            Id = TokenValidationDependencyDocument.GetId(snapshot.TxId),
            TxId = snapshot.TxId
        };

        document.DependsOnTxIds = nextDependencies;
        document.MissingDependencies = snapshot.MissingDependencies.ToArray();
        document.UpdatedAt = DateTimeOffset.UtcNow;

        if (existing is null)
            await session.StoreAsync(document, document.Id, cancellationToken);

        foreach (var dependencyTxId in removedDependencies)
        {
            var reverse = await session.LoadAsync<TokenValidationDependentsDocument>(
                TokenValidationDependentsDocument.GetId(dependencyTxId),
                cancellationToken
            );

            if (reverse is null)
                continue;

            reverse.DirectDependents = reverse.DirectDependents
                .Where(x => !string.Equals(x, snapshot.TxId, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            reverse.UpdatedAt = DateTimeOffset.UtcNow;

            if (reverse.DirectDependents.Length == 0)
                session.Delete(reverse);
        }

        foreach (var dependencyTxId in addedDependencies)
        {
            var reverseDocumentId = TokenValidationDependentsDocument.GetId(dependencyTxId);
            var reverse = await session.LoadAsync<TokenValidationDependentsDocument>(
                reverseDocumentId,
                cancellationToken
            );
            var isNew = reverse is null;
            reverse ??= new TokenValidationDependentsDocument
            {
                Id = reverseDocumentId,
                TxId = dependencyTxId
            };

            reverse.DirectDependents = reverse.DirectDependents
                .Append(snapshot.TxId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            reverse.UpdatedAt = DateTimeOffset.UtcNow;

            if (isNew)
                await session.StoreAsync(reverse, reverse.Id, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public Task UpsertAsync(
        MetaTransaction transaction,
        CancellationToken cancellationToken = default
    ) => UpsertAsync(TokenValidationDependencySnapshot.From(transaction), cancellationToken);

    public async Task<TokenValidationDependencyDocument> LoadAsync(
        string txId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        using var session = documentStore.GetSession();
        return await session.LoadAsync<TokenValidationDependencyDocument>(
            TokenValidationDependencyDocument.GetId(txId),
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<string>> LoadDirectDependentsAsync(
        string txId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        using var session = documentStore.GetSession();
        var document = await session.LoadAsync<TokenValidationDependentsDocument>(
            TokenValidationDependentsDocument.GetId(txId),
            cancellationToken
        );

        return document?.DirectDependents ?? [];
    }

    public async Task RemoveAsync(
        string txId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        using var session = documentStore.GetSession();
        var document = await session.LoadAsync<TokenValidationDependencyDocument>(
            TokenValidationDependencyDocument.GetId(txId),
            cancellationToken
        );

        if (document is null)
            return;

        foreach (var dependencyTxId in document.DependsOnTxIds)
        {
            var reverse = await session.LoadAsync<TokenValidationDependentsDocument>(
                TokenValidationDependentsDocument.GetId(dependencyTxId),
                cancellationToken
            );

            if (reverse is null)
                continue;

            reverse.DirectDependents = reverse.DirectDependents
                .Where(x => !string.Equals(x, txId, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            reverse.UpdatedAt = DateTimeOffset.UtcNow;

            if (reverse.DirectDependents.Length == 0)
                session.Delete(reverse);
        }

        session.Delete(document);
        await session.SaveChangesAsync(cancellationToken);
    }
}
