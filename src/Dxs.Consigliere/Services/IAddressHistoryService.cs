using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;

namespace Dxs.Consigliere.Services;

public interface IAddressHistoryService
{
    Task<AddressHistoryResponse> GetHistory(GetAddressHistoryRequest request);
}
