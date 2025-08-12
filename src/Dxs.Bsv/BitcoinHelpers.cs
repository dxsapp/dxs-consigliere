using System;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using ScriptType = Dxs.Bsv.Script.ScriptType;

namespace Dxs.Bsv;

public static class BitcoinHelpers
{
    public const ulong SatoshisInBsv = 100_000_000;
    public static ulong ToSatoshis(this decimal value) => (ulong)(value * SatoshisInBsv);
    public static decimal SatoshisToBsv(this ulong satoshis) => satoshis / (decimal)SatoshisInBsv;
    public static decimal SatoshisToBsv(this long satoshis) => satoshis / (decimal)SatoshisInBsv;

    public static string GetTxId(ReadOnlySpan<byte> bytes)
    {
        var hash = Hash.Sha256Sha256(bytes);
        var mIdx = hash.Length - 1;

        for (var i = 0; i < hash.Length / 2; i++)
            (hash[i], hash[mIdx - i]) = (hash[mIdx - i], hash[i]);

        return hash.ToHexString();
    }

    public static (string, byte[]) GetTxId2(byte[] hash256)
    {
        var mIdx = hash256.Length - 1;

        for (var i = 0; i < hash256.Length / 2; i++)
            (hash256[i], hash256[mIdx - i]) = (hash256[mIdx - i], hash256[i]);

        return (hash256.ToHexString(), hash256);
    }

    public static Network GetByAddressFirstByte(byte value)
    {
        return value switch
        {
            0x0 => Network.Mainnet,
            0x6f => Network.Testnet,
            0x5 => Network.Mainnet,
            0xc4 => Network.Testnet,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public static string GetP2PkhAddressFromHash160(this string hash160, Network network)
        => GetP2PkhAddressFromHash160(hash160.FromHexString(), network);

    public static string GetP2PkhAddressFromHash160(this ReadOnlySpan<byte> hash160, Network network)
        => GetAddressFromHash160(hash160, ScriptType.P2PKH, network);

    public static string GetAddressFromHash160(this string hash160, ScriptType scriptType, Network network)
        => GetAddressFromHash160(hash160.FromHexString(), scriptType, network);

    public static string GetAddressFromHash160(this ReadOnlySpan<byte> hash160, ScriptType scriptType, Network network)
    {
        if (scriptType == ScriptType.Unknown)
            return null;

        byte prefix = scriptType switch
        {
            ScriptType.P2PKH or ScriptType.Mnee1Sat when network == Network.Mainnet => 0x0,
            ScriptType.P2PKH or ScriptType.Mnee1Sat when network == Network.Testnet => 0x6f,
            ScriptType.P2SH when network == Network.Mainnet => 0x5,
            ScriptType.P2SH when network == Network.Testnet => 0xc4,
            _ => throw new ArgumentOutOfRangeException()
        };

        Span<byte> prefixAddressCheckSum = stackalloc byte[1 + hash160.Length + 4];
        prefixAddressCheckSum[0] = prefix;
        for (var i = 0; i < hash160.Length; i++)
            prefixAddressCheckSum[1 + i] = hash160[i];

        var checksum = Checksum(prefixAddressCheckSum[..21]);
        for (var i = 0; i < checksum.Length; i++)
            prefixAddressCheckSum[1 + hash160.Length + i] = checksum[i];

        return Encoders.Base58.EncodeData(prefixAddressCheckSum);
    }

    public static string GetAddressFromPubKey(ReadOnlySpan<byte> pubKey, ScriptType scriptType, Network network)
    {
        var hash160 = Hash.Sha256Sha256Ripedm160(pubKey);

        return GetAddressFromHash160(hash160, scriptType, network);
    }

    public static byte[] Checksum(ReadOnlySpan<byte> bytes) => Hash.Sha256Sha256(bytes)[..4].ToArray();

    public static byte[] Base58Decode(string encoded) => Encoders.Base58.DecodeData(encoded);
    public static string Base58Encode(byte[] data) => Encoders.Base58.EncodeData(data);
    public static string Base58Encode(ReadOnlySpan<byte> data) => Encoders.Base58.EncodeData(data);
        
    public static string GeneratePrivateKey(Network network)
    {
        var key = new Key();
        var wif = network switch
        {
            Network.Mainnet => key.GetWif(NBitcoin.Network.Main),
            Network.Testnet => key.GetWif(NBitcoin.Network.TestNet),
            _ => throw new ArgumentOutOfRangeException(nameof(network), network, "Unsupported network")
        };

        return wif.ToWif();
    }

    public static Network GetNetworkOrThrow(string network) => GetNetworkOrNull(network) ?? throw new ArgumentException($"Invalid network value: {network}", nameof(network));

    private static Network? GetNetworkOrNull(string network)
    {
        if (network == null)
            return null;

        switch (network.ToLowerInvariant())
        {
            case "main":
            case "btc-mainnet":
            case "mainnet":
                return Network.Mainnet;
            case "testnet":
            case "btc-testnet":
            case "test":
            case "testnet3":
                return Network.Testnet;
            default:
                return null;
        }
    }
}