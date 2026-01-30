namespace Dxs.Consigliere.Dto.Responses;

public class GetTransactionsByBlockResponse
{
    public int BlockHeight { get; set; }
    public Dictionary<string, string> Transactions { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
}
