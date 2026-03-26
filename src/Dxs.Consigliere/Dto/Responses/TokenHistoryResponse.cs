using Dxs.Consigliere.Dto.Responses.History;

namespace Dxs.Consigliere.Dto.Responses;

public class TokenHistoryResponse
{
    public string TokenId { get; set; }
    public TokenHistoryItemResponse[] History { get; set; } = [];
    public int TotalCount { get; set; }
    public TrackedHistoryStatusResponse HistoryStatus { get; set; }
}
