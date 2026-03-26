namespace Dxs.Consigliere.Dto.Responses.History;

public sealed class RootedTokenHistoryStatusResponse
{
    public string[] TrustedRoots { get; set; } = [];
    public int TrustedRootCount { get; set; }
    public int CompletedTrustedRootCount { get; set; }
    public int UnknownRootFindingCount { get; set; }
    public bool RootedHistorySecure { get; set; }
    public bool BlockingUnknownRoot { get; set; }
    public string[] UnknownRootFindings { get; set; } = [];
}
