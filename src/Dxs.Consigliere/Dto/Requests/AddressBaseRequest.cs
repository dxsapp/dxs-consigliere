using System.ComponentModel.DataAnnotations;

namespace Dxs.Consigliere.Dto.Requests;

public record AddressBaseRequest([Required] string Address);