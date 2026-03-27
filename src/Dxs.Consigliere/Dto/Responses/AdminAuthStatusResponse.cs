namespace Dxs.Consigliere.Dto.Responses;

public sealed class AdminAuthStatusResponse
{
    public bool Enabled { get; init; }
    public bool Authenticated { get; init; }
    public string Mode { get; init; } = "cookie";
    public string Username { get; init; }
    public int? SessionTtlMinutes { get; init; }
}
