using Dxs.Bsv;
using Dxs.Bsv.Script;
using Dxs.Consigliere.Data.Indexes;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Dto;

using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Data.Queries;

public static class UtxoQueries
{
    public static IRavenQueryable<MetaOutput> Outputs(this IAsyncDocumentSession session)
        => session.Query<MetaOutput>();

    public static IQueryable<MetaOutput> StasUtxoSet(this IAsyncDocumentSession session)
        => session
            .Query<StasUtxoSetIndex.Result, StasUtxoSetIndex>()
            .OfType<MetaOutput>();

    public static IQueryable<MetaOutput> P2pkhUtxoSet(this IAsyncDocumentSession session)
        => session
            .Outputs()
            .Unspent()
            .P2pkh();

    public static IQueryable<MetaOutput> UtxoSet(this IAsyncDocumentSession session, TokenId tokenId = null)
        => tokenId == null
            ? session.P2pkhUtxoSet()
            : session.StasUtxoSet().ByToken(tokenId);

    public static IQueryable<MetaOutput> P2pkh(this IQueryable<MetaOutput> query)
        => query.Where(x => x.Type == ScriptType.P2PKH);

    public static IQueryable<MetaOutput> Unspent(this IQueryable<MetaOutput> query)
        => query.Where(x => x.Spent != true);

    public static IQueryable<MetaOutput> ByAddress(this IQueryable<MetaOutput> query, Address address)
        => query.Where(x => x.Address == address.Value);

    public static IQueryable<MetaOutput> ByAddresses(this IQueryable<MetaOutput> query, IList<string> address)
        => query.Where(x => x.Address.In(address));

    public static IQueryable<MetaOutput> ByToken(this IQueryable<MetaOutput> query, TokenId tokenId)
        => query.Where(x => x.TokenId == tokenId.Value);

    public static IQueryable<MetaOutput> ByTokens(this IQueryable<MetaOutput> query, IList<string> tokenIds)
        => query.Where(x => x.TokenId.In(tokenIds));

    public static IQueryable<BalanceDto> GetP2PkhBalance(
        this IAsyncDocumentSession session,
        Address address
    ) => session
        .P2pkhUtxoSet()
        .ByAddress(address)
        .SumOutputs();

    public static IQueryable<BalanceDto> GetP2PkhBalances(
        this IAsyncDocumentSession session,
        IList<string> addresses
    ) => session
        .P2pkhUtxoSet()
        .ByAddresses(addresses)
        .SumOutputs();

    private static IQueryable<BalanceDto> SumOutputs(this IQueryable<MetaOutput> query)
        => query
            .GroupBy(x => new { x.Address, x.Spent, x.Type })
            .Select(x => new BalanceDto
            {
                Address = x.Key.Address,
                Satoshis = x.Sum(y => y.Satoshis)
            });
}
