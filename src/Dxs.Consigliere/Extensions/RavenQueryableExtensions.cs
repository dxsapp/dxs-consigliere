using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Extensions;

public static class RavenQueryableExtensions
{
    public static IRavenQueryable<TResult> NoStale<TResult>(
        this IRavenQueryable<TResult> query,
        TimeSpan? timeout = null
    ) => query.Customize(x => x.WaitForNonStaleResults(timeout));
}
