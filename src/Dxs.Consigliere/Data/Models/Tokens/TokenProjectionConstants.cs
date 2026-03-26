namespace Dxs.Consigliere.Data.Models.Tokens;

public static class TokenProjectionProtocolType
{
    public const string Stas = "stas";
    public const string Dstas = "dstas";
}

public static class TokenProjectionValidationStatus
{
    public const string Unknown = "unknown";
    public const string Valid = "valid";
    public const string Invalid = "invalid";
}

public static class TokenProjectionApplicationState
{
    public const string None = "none";
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
}
