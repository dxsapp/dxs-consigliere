namespace Dxs.Consigliere.Dto.Requests;

public class StoreTransactionRequest
{
    public string Raw { get; set; }

    public long Timestamp { get; set; }

    public string RedeemAddress { get; set; }

    public string BlockHash { get; set; }

    public int? BlockHeight { get; set; }

    public int? IndexInBlock { get; set; }
}
