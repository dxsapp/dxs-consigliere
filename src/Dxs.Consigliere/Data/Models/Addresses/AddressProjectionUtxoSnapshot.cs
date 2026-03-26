using Dxs.Bsv.Script;
using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Data.Models.Addresses;

public sealed class AddressProjectionUtxoSnapshot
{
    public string Id { get; set; }
    public string TxId { get; set; }
    public int Vout { get; set; }
    public string Address { get; set; }
    public string TokenId { get; set; }
    public long Satoshis { get; set; }
    public ScriptType ScriptType { get; set; }
    public string ScriptPubKey { get; set; }

    public static AddressProjectionUtxoSnapshot From(MetaOutput output)
        => new()
        {
            Id = output.Id,
            TxId = output.TxId,
            Vout = output.Vout,
            Address = output.Address,
            TokenId = output.TokenId,
            Satoshis = output.Satoshis,
            ScriptType = output.Type,
            ScriptPubKey = output.ScriptPubKey
        };

    public AddressUtxoProjectionDocument ToDocument(long lastSequence)
        => new()
        {
            Id = AddressUtxoProjectionDocument.GetId(TxId, Vout),
            TxId = TxId,
            Vout = Vout,
            Address = Address,
            TokenId = TokenId,
            Satoshis = Satoshis,
            ScriptType = ScriptType,
            ScriptPubKey = ScriptPubKey,
            LastSequence = lastSequence
        };
}
