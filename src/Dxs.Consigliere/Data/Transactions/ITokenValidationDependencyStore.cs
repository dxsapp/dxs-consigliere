using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Data.Transactions;

public interface ITokenValidationDependencyStore
{
    Task UpsertAsync(TokenValidationDependencySnapshot snapshot, CancellationToken cancellationToken = default);
    Task UpsertAsync(MetaTransaction transaction, CancellationToken cancellationToken = default);
    Task<TokenValidationDependencyDocument> LoadAsync(string txId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> LoadDirectDependentsAsync(string txId, CancellationToken cancellationToken = default);
    Task RemoveAsync(string txId, CancellationToken cancellationToken = default);
}
