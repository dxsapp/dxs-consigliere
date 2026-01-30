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

    public static TokenId EnsureValidTokenId(this string value, Network network)
    {
        if (!TokenId.TryParse(value, network, out var tokenId))
            throw new Exception($"Unable to parse TokenId: \"{value}\"");

        return tokenId;
    }
}
