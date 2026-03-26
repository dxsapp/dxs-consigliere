using System.ComponentModel.DataAnnotations;

namespace Dxs.Consigliere.Dto.Requests;

public record WatchAddressRequest(
    [Required] string Address,
    [Required] string Name,
    HistoryPolicyRequest HistoryPolicy = null
) : AddressBaseRequest(Address);
