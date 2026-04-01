namespace Dxs.Consigliere.Dto.Responses.Admin;

public sealed class AdminTrackedAddressSummaryResponse
{
    public long CurrentBsvBalanceSatoshis { get; set; }
    public int TotalUtxoCount { get; set; }
    public int BsvUtxoCount { get; set; }
    public int TokenUtxoCount { get; set; }
    public int TransactionCount { get; set; }
    public long? FirstTransactionAt { get; set; }
    public int? FirstTransactionBlockHeight { get; set; }
    public long? LastTransactionAt { get; set; }
    public int? LastTransactionBlockHeight { get; set; }
    public long? LastProjectionSequence { get; set; }
    public AdminTrackedTokenBalanceSummaryResponse[] TokenBalances { get; set; } = [];
}
