using Dxs.Bsv.Script;

namespace Dxs.Consigliere.Data.Models.Transactions;

public class MetaTransaction
{
    public class Input
    {
        public Input() { }

        public Input(MetaOutput output)
            => (Id, TxId, Vout)
                = (output.Id, output.TxId, output.Vout);

        public string Id { get; set; }
        public string TxId { get; set; }
        public int Vout { get; set; }
    }

    public class Output
    {
        public Output() { }

        public Output(MetaOutput output)
            => (Id, Satoshis, Type, Address, TokenId, Hash160)
                = (output.Id, output.Satoshis, output.Type, output.Address, output.TokenId, output.Hash160);

        public string Id { get; set; }
        public long Satoshis { get; set; }
        public ScriptType Type { get; set; }
        public string Address { get; set; }
        public string TokenId { get; set; }
        public string Hash160 { get; set; }
    }

    public const int DefaultHeight = int.MaxValue;

    public string Id { get; init; }
    public string Block { get; set; }
    public int Height { get; set; }
    public int Index { get; set; }

    public long Timestamp { get; set; }

    public IList<Input> Inputs { get; set; }
    public IList<Output> Outputs { get; set; }

    public List<string> Addresses { get; set; }
    public List<string> TokenIds { get; set; }
    public bool IsStas { get; set; }
    public bool IsIssue { get; set; }
    public bool IsValidIssue { get; set; }
    public bool IsRedeem { get; set; }
    public bool IsWithFee { get; set; }
    public bool IsWithNote { get; set; }

    // public List<string> Roots { get; set; }
    public List<string> IllegalRoots { get; set; }
    public List<string> MissingTransactions { get; set; }

    public bool AllStasInputsKnown { get; set; }
    public string RedeemAddress { get; set; }
    public string StasFrom { get; set; }
    public string Note { get; set; }
}
