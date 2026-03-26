using Dxs.Bsv.Script;
using Dxs.Consigliere.Dto;

namespace Dxs.Consigliere.Data.Models.Addresses;

public sealed class AddressUtxoProjectionDocument
{
    public string Id { get; set; }
    public string TxId { get; set; }
    public int Vout { get; set; }
    public string Address { get; set; }
    public string TokenId { get; set; }
    public long Satoshis { get; set; }
    public ScriptType ScriptType { get; set; }
    public string ScriptPubKey { get; set; }
    public long LastSequence { get; set; }

    public static string GetId(string txId, int vout) => $"address/projection/utxo/{txId}:{vout}";

    public static AddressUtxoProjectionDocument From(AddressProjectionUtxoSnapshot snapshot, long lastSequence)
        => snapshot.ToDocument(lastSequence);

    public UtxoDto ToDto()
        => new()
        {
            Id = Id,
            TxId = TxId,
            Vout = Vout,
            Address = Address,
            TokenId = TokenId,
            Satoshis = Satoshis,
            ScriptPubKey = ScriptPubKey,
            ScriptType = ScriptType
        };

    public AddressProjectionUtxoSnapshot ToSnapshot()
        => new()
        {
            Id = Id,
            TxId = TxId,
            Vout = Vout,
            Address = Address,
            TokenId = TokenId,
            Satoshis = Satoshis,
            ScriptType = ScriptType,
            ScriptPubKey = ScriptPubKey
        };
}
