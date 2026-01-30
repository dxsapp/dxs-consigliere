using Dxs.Bsv;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;

using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Services;

public interface IUtxoManager
{
    Task<List<BalanceDto>> GetBalance(BalanceRequest request);
    Task<List<BalanceDto>> GetBalance(IAsyncDocumentSession session, BalanceRequest request);
    Task<GetUtxoSetResponse> GetUtxoSet(GetUtxoSetRequest request);
    Task<GetUtxoSetResponse> GetUtxoSet(GetUtxoSetBatchRequest request);
    Task<(decimal supply, decimal toBurn)> GetTokenStats(
        TokenId tokenId,
        TokenSchema tokenSchema,
        CancellationToken cancellationToken
    );
}
