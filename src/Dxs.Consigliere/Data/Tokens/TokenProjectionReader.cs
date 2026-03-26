using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Tokens;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Dxs.Consigliere.Extensions;

namespace Dxs.Consigliere.Data.Tokens;

public sealed class TokenProjectionReader(IDocumentStore documentStore)
{
    public async Task<TokenStateProjectionDocument> LoadStateAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        return await session.LoadAsync<TokenStateProjectionDocument>(TokenStateProjectionDocument.GetId(tokenId), cancellationToken);
    }

    public async Task<List<AddressUtxoProjectionDocument>> LoadUtxosAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        return await session.Query<AddressUtxoProjectionDocument>()
            .Where(x => x.TokenId == tokenId)
            .ToListAsync(token: cancellationToken);
    }

    public async Task<List<TokenHistoryProjectionDocument>> LoadHistoryAsync(
        string tokenId,
        int take = 100,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        return await session.Query<TokenHistoryProjectionDocument>()
            .Where(x => x.TokenId == tokenId)
            .OrderByDescending(x => x.Timestamp)
            .Take(take)
            .ToListAsync(token: cancellationToken);
    }
}
