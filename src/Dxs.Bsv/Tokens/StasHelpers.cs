using System;

namespace Dxs.Bsv.Tokens;

public static class StasHelpers
{
    public static string GetTokenIdFromHash160(ReadOnlySpan<byte> hash160) => hash160.ToHexString();

    public static string GetTokenIdFromPublicKey(ReadOnlySpan<byte> publicKey)
        => GetTokenIdFromHash160(Hash.Sha256Sha256Ripedm160(publicKey));

    public static string GetTokenIdFromAddress(Address address) => GetTokenIdFromHash160(address.Hash160);
}