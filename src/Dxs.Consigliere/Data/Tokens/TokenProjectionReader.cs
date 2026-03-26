using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Common.Cache;
using Dxs.Consigliere.Dto;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Dxs.Consigliere.Extensions;

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
                using var session = documentStore.GetNoCacheNoTrackingSession();
                return await session.LoadAsync<TokenStateProjectionDocument>(TokenStateProjectionDocument.GetId(tokenId), ct);
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
                return await session.Query<AddressUtxoProjectionDocument>()
                    .Where(x => x.TokenId == tokenId)
                    .ToListAsync(token: ct);
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
                using var session = documentStore.GetNoCacheNoTrackingSession();
                return await session.Query<AddressBalanceProjectionDocument>()
                    .Where(x => x.TokenId == tokenId)
                    .Select(x => new BalanceDto
                    {
                        Address = x.Address,
                        TokenId = x.TokenId,
                        Satoshis = x.Satoshis
                    })
                    .OrderBy(x => x.Address)
                    .ToListAsync(token: ct);
            },
            cancellationToken);
    }

    public async Task<List<TokenHistoryProjectionDocument>> LoadHistoryAsync(
        string tokenId,
        int take = 100,
        CancellationToken cancellationToken = default
    )
    {
        var descriptor = cacheKeyFactory.CreateTokenHistory(tokenId, take);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            CreateOptions(descriptor),
            async ct =>
            {
                using var session = documentStore.GetNoCacheNoTrackingSession();
                return await session.Query<TokenHistoryProjectionDocument>()
                    .Where(x => x.TokenId == tokenId)
                    .OrderByDescending(x => x.Timestamp)
                    .Take(take)
                    .ToListAsync(token: ct);
            },
            cancellationToken);
    }

    private static ProjectionCacheEntryOptions CreateOptions(ProjectionCacheDescriptor descriptor)
        => new()
        {
            Tags = descriptor.Tags
        };
}
