using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Extensions;
using Dxs.Common.Cache;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Data.Addresses;

public sealed class AddressProjectionReader(
    IDocumentStore documentStore,
    IProjectionReadCache projectionReadCache,
    IProjectionReadCacheKeyFactory cacheKeyFactory
)
{
    public AddressProjectionReader(IDocumentStore documentStore)
        : this(documentStore, new NoopProjectionReadCache(), new ProjectionReadCacheKeyFactory())
    {
    }

    public async Task<List<BalanceDto>> LoadBsvBalancesAsync(
        IReadOnlyCollection<string> addresses,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedAddresses = Normalize(addresses);
        var descriptor = cacheKeyFactory.CreateAddressBalances(normalizedAddresses, null);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            CreateOptions(descriptor),
            async ct =>
            {
                using var session = documentStore.GetNoCacheNoTrackingSession();
                return await session.Query<AddressBalanceProjectionDocument>()
                    .Where(x => x.TokenId == null && x.Address.In(normalizedAddresses))
                    .Select(x => new BalanceDto
                    {
                        Address = x.Address,
                        TokenId = x.TokenId,
                        Satoshis = x.Satoshis
                    })
                    .ToListAsync(token: ct);
            },
            cancellationToken);
    }

    public async Task<List<BalanceDto>> LoadTokenBalancesAsync(
        IReadOnlyCollection<string> addresses,
        IReadOnlyCollection<string> tokenIds,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedAddresses = Normalize(addresses);
        var normalizedTokenIds = Normalize(tokenIds);
        var descriptor = cacheKeyFactory.CreateAddressBalances(normalizedAddresses, normalizedTokenIds);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            CreateOptions(descriptor),
            async ct =>
            {
                using var session = documentStore.GetNoCacheNoTrackingSession();
                return await session.Query<AddressBalanceProjectionDocument>()
                    .Where(x => x.TokenId.In(normalizedTokenIds) && x.Address.In(normalizedAddresses))
                    .Select(x => new BalanceDto
                    {
                        Address = x.Address,
                        TokenId = x.TokenId,
                        Satoshis = x.Satoshis
                    })
                    .ToListAsync(token: ct);
            },
            cancellationToken);
    }

    public async Task<UtxoDto[]> LoadP2pkhUtxosAsync(
        IReadOnlyCollection<string> addresses,
        int take,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedAddresses = Normalize(addresses);
        var descriptor = cacheKeyFactory.CreateAddressBatchUtxos(normalizedAddresses, null, take);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            CreateOptions(descriptor),
            async ct =>
            {
                using var session = documentStore.GetNoCacheNoTrackingSession();
                var results = await session.Query<AddressUtxoProjectionDocument>()
                    .Where(x => x.TokenId == null && x.Address.In(normalizedAddresses))
                    .Take(take)
                    .ToListAsync(token: ct);

                return results.Select(x => x.ToDto()).ToArray();
            },
            cancellationToken);
    }

    public async Task<UtxoDto[]> LoadTokenUtxosAsync(
        IReadOnlyCollection<string> addresses,
        IReadOnlyCollection<string> tokenIds,
        int take,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedAddresses = Normalize(addresses);
        var normalizedTokenIds = Normalize(tokenIds);
        var descriptor = cacheKeyFactory.CreateAddressBatchUtxos(normalizedAddresses, normalizedTokenIds, take);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            CreateOptions(descriptor),
            async ct =>
            {
                using var session = documentStore.GetNoCacheNoTrackingSession();
                var results = await session.Query<AddressUtxoProjectionDocument>()
                    .Where(x => x.TokenId.In(normalizedTokenIds) && x.Address.In(normalizedAddresses))
                    .Take(take)
                    .ToListAsync(token: ct);

                return results.Select(x => x.ToDto()).ToArray();
            },
            cancellationToken);
    }

    public async Task<List<AddressUtxoProjectionDocument>> LoadUtxosAsync(
        string address,
        string tokenId,
        CancellationToken cancellationToken = default
    )
    {
        var descriptor = cacheKeyFactory.CreateAddressUtxos(address, tokenId);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            CreateOptions(descriptor),
            async ct =>
            {
                using var session = documentStore.GetNoCacheNoTrackingSession();
                var query = session.Query<AddressUtxoProjectionDocument>()
                    .Where(x => x.Address == address);

                query = string.IsNullOrWhiteSpace(tokenId)
                    ? query.Where(x => x.TokenId == null)
                    : query.Where(x => x.TokenId == tokenId);

                return await query.ToListAsync(token: ct);
            },
            cancellationToken);
    }

    private static ProjectionCacheEntryOptions CreateOptions(ProjectionCacheDescriptor descriptor)
        => new()
        {
            Tags = descriptor.Tags
        };

    private static string[] Normalize(IReadOnlyCollection<string> values)
        => values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
}
