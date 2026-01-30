using System.ComponentModel.DataAnnotations;

namespace Dxs.Consigliere.Dto.Requests;

public record WatchStasTokenRequest([Required] string TokenId, [Required] string Symbol);
