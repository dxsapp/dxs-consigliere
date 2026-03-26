namespace Dxs.Consigliere.Data.Models.Tokens;

public sealed class TokenProjectionAppliedTransactionDocument
{
    public string Id { get; set; }
    public string TxId { get; set; }
    public string AppliedState { get; set; } = TokenProjectionApplicationState.None;
    public string ConfirmedBlockHash { get; set; }
    public string[] TokenIds { get; set; } = [];
    public string[] HistoryDocumentIds { get; set; } = [];
    public DateTimeOffset? LastObservedAt { get; set; }
    public long LastSequence { get; set; }

    public static string GetId(string txId) => $"token/projection/applied/{txId}";
}
