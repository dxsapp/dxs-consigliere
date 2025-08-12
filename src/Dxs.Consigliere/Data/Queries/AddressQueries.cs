using Dxs.Bsv;
using Dxs.Consigliere.Data.Models;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Data.Queries;

public static class AddressQueries
{
    public static IRavenQueryable<WatchingAddress> WatchingAddresses(this IAsyncDocumentSession session)
        => session
            .Query<WatchingAddress>();

    public static IRavenQueryable<WatchingAddress> WatchingAddressByAddress(this IAsyncDocumentSession session, Address address)
        => session
            .WatchingAddresses()
            .Where(x => x.Address == address.Value);
}