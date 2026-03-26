namespace Dxs.Consigliere.Data.Models.Tokens;

public sealed class TokenStateProjectionDocument
{
    public string Id { get; set; }
    public string TokenId { get; set; }
    public string ProtocolType { get; set; }
    public string ProtocolVersion { get; set; }
    public bool IssuanceKnown { get; set; }
    public string ValidationStatus { get; set; } = TokenProjectionValidationStatus.Unknown;
    public string Issuer { get; set; }
    public string RedeemAddress { get; set; }
    public long? TotalKnownSupply { get; set; }
    public long? BurnedSatoshis { get; set; }
    public int? LastIndexedHeight { get; set; }
    public long LastSequence { get; set; }

    public static string GetId(string tokenId) => $"token/projection/state/{tokenId}";
}
