namespace Dxs.Consigliere.Dto.Requests;

public sealed class TokenHistoryPolicyRequest
{
    public string[] TrustedRoots { get; set; } = [];
}
