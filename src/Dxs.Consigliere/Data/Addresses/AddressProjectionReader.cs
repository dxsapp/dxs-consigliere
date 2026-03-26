using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Data.Addresses;

public sealed class AddressProjectionReader(IDocumentStore documentStore)
{
    public async Task<List<BalanceDto>> LoadBsvBalancesAsync(
        IReadOnlyCollection<string> addresses,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        return await session.Query<AddressBalanceProjectionDocument>()
            .Where(x => x.TokenId == null && x.Address.In(addresses))
            .Select(x => new BalanceDto
            {
                Address = x.Address,
                TokenId = x.TokenId,
                Satoshis = x.Satoshis
            })
            .ToListAsync(token: cancellationToken);
    }

    public async Task<List<BalanceDto>> LoadTokenBalancesAsync(
        IReadOnlyCollection<string> addresses,
        IReadOnlyCollection<string> tokenIds,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        return await session.Query<AddressBalanceProjectionDocument>()
            .Where(x => x.TokenId.In(tokenIds) && x.Address.In(addresses))
            .Select(x => new BalanceDto
            {
                Address = x.Address,
                TokenId = x.TokenId,
                Satoshis = x.Satoshis
            })
            .ToListAsync(token: cancellationToken);
    }

    public async Task<UtxoDto[]> LoadP2pkhUtxosAsync(
        IReadOnlyCollection<string> addresses,
        int take,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var results = await session.Query<AddressUtxoProjectionDocument>()
            .Where(x => x.TokenId == null && x.Address.In(addresses))
            .Take(take)
            .ToListAsync(token: cancellationToken);

        return results.Select(x => x.ToDto()).ToArray();
    }

    public async Task<UtxoDto[]> LoadTokenUtxosAsync(
        IReadOnlyCollection<string> addresses,
        IReadOnlyCollection<string> tokenIds,
        int take,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var results = await session.Query<AddressUtxoProjectionDocument>()
            .Where(x => x.TokenId.In(tokenIds) && x.Address.In(addresses))
            .Take(take)
            .ToListAsync(token: cancellationToken);

        return results.Select(x => x.ToDto()).ToArray();
    }

    public async Task<List<AddressUtxoProjectionDocument>> LoadUtxosAsync(
        string address,
        string tokenId,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var query = session.Query<AddressUtxoProjectionDocument>()
            .Where(x => x.Address == address);

        query = string.IsNullOrWhiteSpace(tokenId)
            ? query.Where(x => x.TokenId == null)
            : query.Where(x => x.TokenId == tokenId);

        return await query.ToListAsync(token: cancellationToken);
    }
}
