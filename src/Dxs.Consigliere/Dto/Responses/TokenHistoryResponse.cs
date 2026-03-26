namespace Dxs.Consigliere.Dto.Responses;

public class TokenHistoryResponse
{
    public string TokenId { get; set; }
    public TokenHistoryItemResponse[] History { get; set; } = [];
    public int TotalCount { get; set; }
}
