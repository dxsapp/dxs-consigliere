using System.ComponentModel.DataAnnotations;

namespace Dxs.Consigliere.Dto.Requests;

public record GetAddressHistoryRequest(
    [Required] string Address,
    string[] TokenIds,
    bool Desc,
    bool SkipZeroBalance,
    [Required] int Skip,
    [Required] int Take
): PageRequest(Skip, Take, Desc);