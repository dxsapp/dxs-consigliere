using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Data.Transactions;

public class DeletedTransaction
{
    public string Id { get; set; }
    public DateTime DeletedAt { get; set; }
    public MetaTransaction MetaTransaction { get; set; }
    public List<MetaOutput> MetaOutputs { get; set; }
    public string RawData { get; set; }

    public static string GetId(string txId) => $"tx/deleted/{txId}";
}
