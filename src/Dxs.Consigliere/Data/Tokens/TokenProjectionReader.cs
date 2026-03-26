using Dxs.Common.Cache;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Tokens.Dstas;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Data.Tokens;

public sealed class TokenProjectionReader(
    IDocumentStore documentStore,
    IProjectionReadCache projectionReadCache,
    IProjectionReadCacheKeyFactory cacheKeyFactory
)
{
    public TokenProjectionReader(IDocumentStore documentStore)
        : this(documentStore, new NoopProjectionReadCache(), new ProjectionReadCacheKeyFactory())
    {
    }

    public async Task<TokenStateProjectionDocument> LoadStateAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        var descriptor = cacheKeyFactory.CreateTokenState(tokenId);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            CreateOptions(descriptor),
            async ct =>
            {
                var rooted = await LoadRootedContextAsync(tokenId, ct);
                if (rooted is null)
                {
                    using var noCacheSession = documentStore.GetNoCacheNoTrackingSession();
                    return await noCacheSession.LoadAsync<TokenStateProjectionDocument>(TokenStateProjectionDocument.GetId(tokenId), ct);
                }

                using var rootedSession = documentStore.GetNoCacheNoTrackingSession();
                var utxos = await rootedSession.Query<AddressUtxoProjectionDocument>()
                    .Where(x => x.TokenId == tokenId)
                    .ToListAsync(token: ct);

                var canonicalTxIds = rooted.CanonicalTxIds;
                var rootedUtxos = utxos
                    .Where(x => canonicalTxIds.Contains(x.TxId))
                    .ToArray();

                if (rooted.CanonicalTransactions.Length == 0)
                    return null;

                var issuance = rooted.CanonicalTransactions
                    .Where(x => x.IsIssue)
                    .OrderBy(x => x.Height)
                    .ThenBy(x => x.Index)
                    .FirstOrDefault();
                var redeemAddress = issuance?.RedeemAddress;
                var burned = !string.IsNullOrWhiteSpace(redeemAddress)
                    ? rootedUtxos.Where(x => string.Equals(x.Address, redeemAddress, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Satoshis)
                    : 0L;
                var supply = !string.IsNullOrWhiteSpace(redeemAddress)
                    ? rootedUtxos.Where(x => !string.Equals(x.Address, redeemAddress, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Satoshis)
                    : rootedUtxos.Sum(x => x.Satoshis);
                var anyInvalid = rooted.CanonicalTransactions.Any(x => string.Equals(StasProtocolProjectionSemantics.GetValidationStatus(x), TokenProjectionValidationStatus.Invalid, StringComparison.Ordinal));

                return new TokenStateProjectionDocument
                {
                    Id = TokenStateProjectionDocument.GetId(tokenId),
                    TokenId = tokenId,
                    ProtocolType = rooted.CanonicalTransactions.Select(StasProtocolProjectionSemantics.GetProtocolType).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                    ProtocolVersion = null,
                    IssuanceKnown = issuance is not null,
                    ValidationStatus = anyInvalid
                        ? TokenProjectionValidationStatus.Invalid
                        : issuance is null || !rooted.Evaluation.RootedHistorySecure
                            ? TokenProjectionValidationStatus.Unknown
                            : TokenProjectionValidationStatus.Valid,
                    Issuer = issuance?.RedeemAddress,
                    RedeemAddress = redeemAddress,
                    TotalKnownSupply = supply,
                    BurnedSatoshis = burned,
                    LastIndexedHeight = rooted.CanonicalTransactions
                        .Where(x => x.Height != MetaTransaction.DefaultHeight)
                        .Select(x => (int?)x.Height)
                        .Max(),
                    LastSequence = 0
                };
            },
            cancellationToken);
    }

    public async Task<List<AddressUtxoProjectionDocument>> LoadUtxosAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        var descriptor = cacheKeyFactory.CreateTokenUtxos(tokenId);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            CreateOptions(descriptor),
            async ct =>
            {
                using var session = documentStore.GetNoCacheNoTrackingSession();
                var utxos = await session.Query<AddressUtxoProjectionDocument>()
                    .Where(x => x.TokenId == tokenId)
                    .ToListAsync(token: ct);

                var rooted = await LoadRootedContextAsync(tokenId, ct);
                return rooted is null
                    ? utxos
                    : utxos.Where(x => rooted.CanonicalTxIds.Contains(x.TxId)).ToList();
            },
            cancellationToken);
    }

    public async Task<List<BalanceDto>> LoadBalancesAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        var descriptor = cacheKeyFactory.CreateTokenBalances(tokenId);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            CreateOptions(descriptor),
            async ct =>
            {
                var rooted = await LoadRootedContextAsync(tokenId, ct);
                if (rooted is null)
                {
                    using var noCacheSession = documentStore.GetNoCacheNoTrackingSession();
                    return await noCacheSession.Query<AddressBalanceProjectionDocument>()
                        .Where(x => x.TokenId == tokenId)
                        .Select(x => new BalanceDto
                        {
                            Address = x.Address,
                            TokenId = x.TokenId,
                            Satoshis = x.Satoshis
                        })
                        .OrderBy(x => x.Address)
                        .ToListAsync(token: ct);
                }

                var utxos = await LoadUtxosAsync(tokenId, ct);
                return utxos
                    .GroupBy(x => x.Address, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new BalanceDto
                    {
                        Address = x.Key,
                        TokenId = tokenId,
                        Satoshis = x.Sum(y => y.Satoshis)
                    })
                    .OrderBy(x => x.Address, StringComparer.Ordinal)
                    .ToList();
            },
            cancellationToken);
    }

    public async Task<List<TokenHistoryProjectionDocument>> LoadHistoryAsync(
        string tokenId,
        int take = 100,
        CancellationToken cancellationToken = default
    ) => await LoadHistoryAsync(tokenId, 0, take, true, cancellationToken);

    public async Task<List<TokenHistoryProjectionDocument>> LoadHistoryAsync(
        string tokenId,
        int skip,
        int take,
        bool desc,
        CancellationToken cancellationToken = default
    )
    {
        var descriptor = cacheKeyFactory.CreateTokenHistory(tokenId, take, skip, desc);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            CreateOptions(descriptor),
            async ct =>
            {
                using var session = documentStore.GetNoCacheNoTrackingSession();
                var history = await session.Query<TokenHistoryProjectionDocument>()
                    .Where(x => x.TokenId == tokenId)
                    .ToListAsync(token: ct);

                var rooted = await LoadRootedContextAsync(tokenId, ct);
                if (rooted is not null)
                    history = history.Where(x => rooted.CanonicalTxIds.Contains(x.TxId)).ToList();

                var ordered = desc
                    ? history.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.TxId, StringComparer.Ordinal)
                    : history.OrderBy(x => x.Timestamp).ThenBy(x => x.TxId, StringComparer.Ordinal);

                return ordered
                    .Skip(Math.Max(0, skip))
                    .Take(take)
                    .ToList();
            },
            cancellationToken);
    }

    public async Task<int> CountHistoryAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        var rooted = await LoadRootedContextAsync(tokenId, cancellationToken);
        using var session = documentStore.GetNoCacheNoTrackingSession();
        if (rooted is null)
        {
            return await session.Query<TokenHistoryProjectionDocument>()
                .Where(x => x.TokenId == tokenId)
                .CountAsync(token: cancellationToken);
        }

        var history = await session.Query<TokenHistoryProjectionDocument>()
            .Where(x => x.TokenId == tokenId)
            .ToListAsync(token: cancellationToken);
        return history.Count(x => rooted.CanonicalTxIds.Contains(x.TxId));
    }

    private async Task<RootedTokenReadContext?> LoadRootedContextAsync(string tokenId, CancellationToken cancellationToken)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken);
        var trustedRoots = status?.HistorySecurity?.TrustedRoots ?? [];
        if (!string.Equals(status?.HistoryMode, TrackedEntityHistoryMode.FullHistory, StringComparison.Ordinal)
            || trustedRoots.Length == 0)
            return null;

        var transactions = await session.Query<MetaTransaction>()
            .Where(x => x.TokenIds.Contains(tokenId))
            .ToListAsync(token: cancellationToken);
        var evaluation = TrackedTokenRootedHistoryEvaluator.Evaluate(tokenId, trustedRoots, transactions);
        return new RootedTokenReadContext(
            evaluation,
            new HashSet<string>(evaluation.CanonicalTxIds, StringComparer.OrdinalIgnoreCase),
            transactions.Where(x => evaluation.CanonicalTxIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToArray());
    }

    private static ProjectionCacheEntryOptions CreateOptions(ProjectionCacheDescriptor descriptor)
        => new()
        {
            Tags = descriptor.Tags
        };

    private sealed record RootedTokenReadContext(
        TrackedTokenRootedHistoryEvaluation Evaluation,
        HashSet<string> CanonicalTxIds,
        MetaTransaction[] CanonicalTransactions);
}
