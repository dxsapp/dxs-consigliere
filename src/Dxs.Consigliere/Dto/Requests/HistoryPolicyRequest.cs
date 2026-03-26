namespace Dxs.Consigliere.Dto.Requests;

public sealed class HistoryPolicyRequest
{
    public string Mode { get; set; } = HistoryPolicyMode.ForwardOnly;
}

public static class HistoryPolicyMode
{
    public const string ForwardOnly = "forward_only";
    public const string FullHistory = "full_history";
}
