using Dxs.Bsv.Script;
using Dxs.Consigliere.Data.Models.Transactions;
using Raven.Client.Documents.Indexes;

namespace Dxs.Consigliere.Data.Indexes;

public class StasUtxoSetIndex: AbstractIndexCreationTask<MetaOutput>
{
    public class Result
    {
        public string Id { get; set; }
        public  string TxId { get; set; }
        public string Address { get; set; }
        public string TokenId { get; set; }
        public long Satoshis { get; set; }
    }

    public StasUtxoSetIndex()
    {
        Map = outputs =>
            from output in outputs
            where output.Type == ScriptType.P2STAS
                && output.Spent != true

            let tx = LoadDocument<MetaTransaction>(output.TxId)

            where tx.IsValidIssue || (tx.AllStasInputsKnown && tx.IllegalRoots.Count == 0)
            select new Result
            {
                Id = output.Id,
                TxId = tx.Id,
                Address = output.Address,
                TokenId = output.TokenId,
                Satoshis = output.Satoshis
            };
    }
}