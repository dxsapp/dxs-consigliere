using Dxs.Consigliere.Data.Models.Tokens;

namespace Dxs.Consigliere.Dto.Responses;

public class TokenHistoryItemResponse
{
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

    public static TokenHistoryItemResponse From(TokenHistoryProjectionDocument document) => new()
    {
        TokenId = document.TokenId,
        TxId = document.TxId,
        Timestamp = document.Timestamp,
        Height = document.Height,
        ReceivedSatoshis = document.ReceivedSatoshis,
        SpentSatoshis = document.SpentSatoshis,
        BalanceDeltaSatoshis = document.BalanceDeltaSatoshis,
        IsIssue = document.IsIssue,
        IsRedeem = document.IsRedeem,
        ValidationStatus = document.ValidationStatus,
        ProtocolType = document.ProtocolType,
        ConfirmedBlockHash = document.ConfirmedBlockHash
    };
}
