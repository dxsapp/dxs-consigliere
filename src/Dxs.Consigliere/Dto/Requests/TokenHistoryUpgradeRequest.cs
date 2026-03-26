using System.ComponentModel.DataAnnotations;

namespace Dxs.Consigliere.Dto.Requests;

public sealed class TokenHistoryUpgradeRequest
{
    [Required]
    public string TokenId { get; set; }

    public TokenHistoryPolicyRequest TokenHistoryPolicy { get; set; } = new();
}
