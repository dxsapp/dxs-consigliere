using Dxs.Consigliere.Dto.Responses.History;

namespace Dxs.Consigliere.Dto.Responses;

public class AddressHistoryResponse
{
    public AddressHistoryDto[] History { get; set; }
    public int TotalCount { get; set; }
    public TrackedHistoryStatusResponse HistoryStatus { get; set; }
}
