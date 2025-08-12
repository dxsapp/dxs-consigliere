using Dxs.Bsv;

namespace Dxs.Consigliere.Extensions;

public static class StringExtensions
{
    public static Address EnsureValidBsvAddress(this string value)
    {
        if (!Address.TryParse(value, out var address))
            throw new Exception($"BSV address provided has invalid format: {value}");

        return address;
    }

    public static TokenId EnsureValidTokenId(this string value)
    {
        if (!TokenId.TryParse(value, Network.Mainnet, out var tokenId))
            throw new Exception($"Unable to parse TokenId: \"{value}\"");

        return tokenId;
    }
}