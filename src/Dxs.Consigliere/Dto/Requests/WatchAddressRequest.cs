using System.ComponentModel.DataAnnotations;

namespace Dxs.Consigliere.Dto.Requests;

public record WatchAddressRequest([Required] string Address, [Required] string Name): AddressBaseRequest(Address);