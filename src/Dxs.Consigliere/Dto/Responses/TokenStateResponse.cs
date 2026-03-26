using Dxs.Consigliere.Data.Models.Tokens;

namespace Dxs.Consigliere.Dto.Responses;

public class TokenStateResponse
{
    public string TokenId { get; set; }
    public string ProtocolType { get; set; }
    public string ProtocolVersion { get; set; }
    public bool IssuanceKnown { get; set; }
    public string ValidationStatus { get; set; }
    public string Issuer { get; set; }
    public string RedeemAddress { get; set; }
    public long? TotalKnownSupply { get; set; }
    public long? BurnedSatoshis { get; set; }
    public int? LastIndexedHeight { get; set; }

    public static TokenStateResponse From(TokenStateProjectionDocument document) => new()
    {
        TokenId = document.TokenId,
        ProtocolType = document.ProtocolType,
        ProtocolVersion = document.ProtocolVersion,
        IssuanceKnown = document.IssuanceKnown,
        ValidationStatus = document.ValidationStatus,
        Issuer = document.Issuer,
        RedeemAddress = document.RedeemAddress,
        TotalKnownSupply = document.TotalKnownSupply,
        BurnedSatoshis = document.BurnedSatoshis,
        LastIndexedHeight = document.LastIndexedHeight
    };
}
