using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Raven.Client.Documents.Indexes;

namespace Dxs.Consigliere.Data.Indexes;

public class FoundMissingRootsIndex: AbstractIndexCreationTask<MetaTransaction, FoundMissingTransaction>
{
    public FoundMissingRootsIndex()
    {
        Map = txs =>
            from tx in txs

            let fanOut = tx.MissingTransactions
                .Select(missing => new
                {
                    TxId = tx.Id,
                    MissingTxId = missing,
                    MissingCount = tx.MissingTransactions.Count
                })
                .Concat(new[]
                {
                    new
                    {
                        TxId = tx.MissingTransactions.Count > 0 ? tx.Id : null,
                        MissingTxId = tx.MissingTransactions.Count > 0 ? null : tx.Id,
                        MissingCount = tx.MissingTransactions.Count
                    }
                })
                .ToArray()


            from x in fanOut
            where x.MissingTxId != null
            select new FoundMissingTransaction
            {
                TxId = x.TxId,
                MissingTxId = x.MissingTxId,
                MissingCount = x.MissingCount
            };

        Reduce = results =>
                from x in results
                group x by x.MissingTxId
                into g
                where g.Count() > 1
                    && g.Any(y => y.MissingCount == 0)

                from tx in g.Where(y => y.TxId != null)
                select new FoundMissingTransaction
                {
                    TxId = tx.TxId,
                    MissingTxId = g.Key,
                    MissingCount = g.Sum(y => y.MissingCount)
                }
            ;

        OutputReduceToCollection = "FoundMissingTransactions";
        PatternReferencesCollectionName = "FoundMissingTransactions/References";
        PatternForOutputReduceToCollectionReferences = x => $"{x.TxId}/{x.MissingTxId}/found";
    }
}