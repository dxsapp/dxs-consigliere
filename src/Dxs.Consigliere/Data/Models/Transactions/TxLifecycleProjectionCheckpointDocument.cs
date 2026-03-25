namespace Dxs.Consigliere.Data.Models.Transactions;

public class TxLifecycleProjectionCheckpointDocument
{
    public const string DocumentId = "tx/lifecycle/checkpoints/default";

    public string Id { get; set; } = DocumentId;
    public long LastSequence { get; set; }
    public string LastFingerprint { get; set; }
}
