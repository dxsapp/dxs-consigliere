namespace Dxs.Consigliere.Data.Models;

public class MissingTransaction: Entity
{
    public string TxId { get; set; } 
        
    public override string GetId() => GetId(TxId);
    public static string GetId(string txId) => $"MissingTransaction/{txId}";

    public override IEnumerable<string> AllKeys() => [nameof(TxId)];

    public override IEnumerable<string> UpdateableKeys() => Array.Empty<string>();

    public override IEnumerable<KeyValuePair<string, object>> ToEntries() => [new(nameof(TxId), TxId)];
}