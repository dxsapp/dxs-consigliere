namespace Dxs.Consigliere.Data.Models.Tokens;

public sealed class TokenHistoryProjectionDocument
{
    public string Id { get; set; }
    public string TokenId { get; set; }
    public string TxId { get; set; }
    public long Timestamp { get; set; }
    public int Height { get; set; }
    public long ReceivedSatoshis { get; set; }
    public long SpentSatoshis { get; set; }
    public long BalanceDeltaSatoshis { get; set; }
    public bool IsIssue { get; set; }
    public bool IsRedeem { get; set; }
    public string ValidationStatus { get; set; }
    public string ProtocolType { get; set; }
    public string ConfirmedBlockHash { get; set; }
    public long LastSequence { get; set; }

    public static string GetId(string tokenId, string txId) => $"token/projection/history/{tokenId}/{txId}";
}
