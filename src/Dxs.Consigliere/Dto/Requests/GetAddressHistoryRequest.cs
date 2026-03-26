using System.ComponentModel.DataAnnotations;

namespace Dxs.Consigliere.Dto.Requests;

public record GetAddressHistoryRequest(
    [Required] string Address,
    string[] TokenIds,
    bool Desc,
    bool SkipZeroBalance,
    [Required] int Skip = 0,
    [Required] int Take = 100,
    bool AcceptPartialHistory = false
) : PageRequest(Skip, Take, Desc);
