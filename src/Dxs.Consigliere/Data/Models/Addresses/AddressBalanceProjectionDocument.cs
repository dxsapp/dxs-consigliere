namespace Dxs.Consigliere.Data.Models.Addresses;

public sealed class AddressBalanceProjectionDocument
{
    public string Id { get; set; }
    public string Address { get; set; }
    public string TokenId { get; set; }
    public long Satoshis { get; set; }
    public long LastSequence { get; set; }

    public static string GetId(string address, string tokenId)
        => $"address/projection/balance/{address}/{GetTokenKey(tokenId)}";

    public static string GetTokenKey(string tokenId)
        => string.IsNullOrWhiteSpace(tokenId)
            ? AddressProjectionConstants.BsvTokenKey
            : tokenId;
}
