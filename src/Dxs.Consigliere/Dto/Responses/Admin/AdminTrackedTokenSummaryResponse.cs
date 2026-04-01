namespace Dxs.Consigliere.Dto.Responses.Admin;

public sealed class AdminTrackedTokenSummaryResponse
{
    public string ProtocolType { get; set; }
    public string ValidationStatus { get; set; }
    public string Issuer { get; set; }
    public string RedeemAddress { get; set; }
    public long? LocalKnownSupplySatoshis { get; set; }
    public long? BurnedSatoshis { get; set; }
    public int HolderCount { get; set; }
    public int UtxoCount { get; set; }
    public int TransactionCount { get; set; }
    public long? FirstTransactionAt { get; set; }
    public int? FirstTransactionBlockHeight { get; set; }
    public long? LastTransactionAt { get; set; }
    public int? LastTransactionBlockHeight { get; set; }
    public long? LastProjectionSequence { get; set; }
}
