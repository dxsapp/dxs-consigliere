using Dxs.Consigliere.Data.Models.Transactions;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Data.Queries;

public static class TransactionsQueries
{
    public static IRavenQueryable<MetaTransaction> Transactions(
        this IAsyncDocumentSession session
    ) => session.Query<MetaTransaction>();
}
