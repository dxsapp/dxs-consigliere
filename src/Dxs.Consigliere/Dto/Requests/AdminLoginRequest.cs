namespace Dxs.Consigliere.Dto.Requests;

public sealed class AdminLoginRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
