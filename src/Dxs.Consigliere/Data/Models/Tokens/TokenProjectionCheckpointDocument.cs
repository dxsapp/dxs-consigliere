namespace Dxs.Consigliere.Data.Models.Tokens;

public sealed class TokenProjectionCheckpointDocument
{
    public const string DocumentId = "token/projection/checkpoints/default";

    public string Id { get; set; } = DocumentId;
    public long LastSequence { get; set; }
    public string LastFingerprint { get; set; }
}
