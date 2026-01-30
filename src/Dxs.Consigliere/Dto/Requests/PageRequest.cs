using System.ComponentModel.DataAnnotations;

namespace Dxs.Consigliere.Dto.Requests;

public record PageRequest([Required] int Skip, [Required] int Take, [Required] bool Desc);
