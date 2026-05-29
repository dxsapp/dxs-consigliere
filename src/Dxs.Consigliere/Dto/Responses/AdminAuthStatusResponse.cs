#nullable enable
namespace Dxs.Consigliere.Dto.Responses;

public sealed class AdminAuthStatusResponse
{
    public bool SetupRequired { get; init; }
    public bool Enabled { get; init; }
    public bool Authenticated { get; init; }
    public string Mode { get; init; } = "cookie";
    // Always set by AdminAuthController — empty string when anonymous.
    public string Username { get; init; } = string.Empty;
    public int? SessionTtlMinutes { get; init; }
}
