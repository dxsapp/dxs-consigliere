namespace Dxs.Consigliere.Data.Models.Addresses;

public sealed class AddressProjectionCheckpointDocument
{
    public const string DocumentId = "address/projection/checkpoints/default";

    public string Id { get; set; } = DocumentId;
    public long LastSequence { get; set; }
    public string LastFingerprint { get; set; }
}
