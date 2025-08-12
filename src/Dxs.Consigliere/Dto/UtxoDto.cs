using Dxs.Bsv.Script;
using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Dto;

public class UtxoDto
{
    // db constructor
    public UtxoDto() {}
    
    public UtxoDto(MetaOutput output) =>
        (Id, TxId, Vout, Address, Satoshis, TokenId, ScriptPubKey, ScriptType) =
        (output.Id, output.TxId, output.Vout, output.Address, output.Satoshis, output.TokenId, output.ScriptPubKey, output.Type);
    
    public string Id { get; init; }
    public string TxId { get; init; }
    public long Vout { get; init; }

    public string Address { get; init; }
    public string TokenId { get; init; }
    public long Satoshis { get; init; }

    public string ScriptPubKey { get; init; }
    public ScriptType ScriptType { get; init; }
}