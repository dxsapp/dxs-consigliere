using Dxs.Bsv.Script;
using Dxs.Consigliere.Data.Models.History;
using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Data.Indexes;

using Raven.Client.Documents.Indexes;

public class AddressHistoryIndex : AbstractIndexCreationTask<MetaTransaction, AddressHistory>
{
    public AddressHistoryIndex()
    {
        Map = txs =>
            from tx in txs

            let metaInputs = LoadDocument<MetaOutput>(tx.Inputs.Select(x => x.Id))
            let inputs = metaInputs
                .Select(x => new AddressHistory
                {
                    Address = x.Address,
                    TokenId = x.TokenId,
                    TxId = tx.Id,
                    ScriptType = x.Type,
                    Timestamp = tx.Timestamp,
                    Height = tx.Height,
                    SpentSatoshis = x.Satoshis,
                    ReceivedSatoshis = 0,
                    BalanceSatoshis = -x.Satoshis,
                    ValidStasTx = true,
                    Note = tx.Note,
                    Side = -1,
                })
            let outputs = tx.Outputs
                .Where(x => x.Type == ScriptType.P2PKH || x.Type == ScriptType.P2STAS)
                .Select(x => new AddressHistory
                {
                    Address = x.Address,
                    TokenId = x.TokenId,
                    TxId = tx.Id,
                    ScriptType = x.Type,
                    Timestamp = tx.Timestamp,
                    Height = tx.Height,
                    SpentSatoshis = 0,
                    ReceivedSatoshis = x.Satoshis,
                    BalanceSatoshis = x.Satoshis,
                    ValidStasTx = tx.IsIssue
                        ? tx.IsValidIssue
                        : !tx.IllegalRoots.Any(),
                    Note = tx.Note,
                    Side = 1,
                })

            from x in inputs.Concat(outputs)
            where x.Address != null
            select new AddressHistory
            {
                Address = x.Address,
                TokenId = x.TokenId,
                TxId = x.TxId,
                Timestamp = x.Timestamp,
                Height = x.Height,
                SpentSatoshis = x.SpentSatoshis,
                ReceivedSatoshis = x.ReceivedSatoshis,
                BalanceSatoshis = x.BalanceSatoshis,
                TxFeeSatoshis = inputs.Sum(x => x.SpentSatoshis) - outputs.Sum(x => x.ReceivedSatoshis),
                ValidStasTx = x.ValidStasTx,
                Note = x.Note,
                Side = x.Side,
                FromAddresses = metaInputs
                    .Where(y => y.Address != x.Address)
                    .Select(y => y.Address)
                    .Take(16)
                    .ToHashSet(),
                ToAddresses = tx.Outputs
                    .Where(y => y.Address != x.Address)
                    .Select(y => y.Address)
                    .Take(16)
                    .ToHashSet()
            };

        Reduce = items =>
            from item in items
            group item by new
            {
                item.TxId,
                item.Address,
                item.TokenId
            }
            into g
            let entity = g.First()
            select new AddressHistory
            {
                Address = g.Key.Address,
                TokenId = g.Key.TokenId,
                TxId = g.Key.TxId,
                Timestamp = entity.Timestamp,
                Height = entity.Height,
                SpentSatoshis = g.Sum(x => x.SpentSatoshis),
                ReceivedSatoshis = g.Sum(x => x.ReceivedSatoshis),
                BalanceSatoshis = g.Sum(x => x.BalanceSatoshis),
                TxFeeSatoshis = g.Min(x => x.TxFeeSatoshis),
                ValidStasTx = entity.ValidStasTx,
                Note = entity.Note,
                Side = 0,
                FromAddresses = entity.FromAddresses,
                ToAddresses = entity.ToAddresses
            };
    }
}
