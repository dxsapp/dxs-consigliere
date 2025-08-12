using System;
using Dxs.Bsv.Script;

namespace Dxs.Bsv;

public class TokenId
{
    private TokenId(string value, Address redeemAddress)
    {
        Value = value;
        RedeemAddress = redeemAddress;
    }
    public string Value { get; }
    public Address RedeemAddress { get; }

    public static bool TryParse(string value, Network network, out TokenId tokenId)
    {
        tokenId = null;

        if (value is { Length: 40 } valid)
        {
            try
            {
                var hash160 = valid.FromHexString();

                tokenId = new TokenId(valid, new(hash160, ScriptType.P2PKH, network));

                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    public static TokenId Parse(string value, Network network)
    {
        if (TryParse(value, network, out var tokenId))
            return tokenId;

        throw new Exception($"Malformed token Id: {value}");
    }

    public override string ToString() => Value;
}