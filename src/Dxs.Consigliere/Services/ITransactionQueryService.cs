using Dxs.Consigliere.Dto.Responses;

namespace Dxs.Consigliere.Services;

public interface ITransactionQueryService
{
    Task<string> GetTransactionAsync(string id, CancellationToken cancellationToken = default);

    Task<Dictionary<string, string>> GetTransactionsAsync(
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default
    );

    Task<GetTransactionsByBlockResponse> GetTransactionsByBlockAsync(
        int blockHeight,
        int skip,
        CancellationToken cancellationToken = default
    );

    Task<ValidateStasResponse> ValidateStasTransactionAsync(
        string id,
        CancellationToken cancellationToken = default
    );
}
