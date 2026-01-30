using System;

using Dxs.Bsv.Extensions;

using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;

using ScriptType = Dxs.Bsv.Script.ScriptType;

namespace Dxs.Bsv;

public class PrivateKey : IEquatable<PrivateKey>
{
    private readonly ECPrivKey _privateKey;
    private readonly ECPubKey _publicKey;

    public PrivateKey(Network network)
        : this(new Key().GetWif(network.ToNBitcoin()).ToString(), network) { }

    public PrivateKey(string privateKey, Network network)
    {
        var (pk, n) = ParsePrivateKey(privateKey);

        _privateKey = pk;
        _publicKey = pk.CreatePubKey();

        PublicKey = _publicKey.ToBytes();

        if (n is { } networkValue && networkValue != network)
            throw new Exception("PrivateKey belongs to other network");

        Network = network;
    }

    public byte[] PublicKey { get; }
    public Network Network { get; }

    public Address P2PkhAddress => new(Hash160, ScriptType.P2PKH, Network);

    public byte[] Hash160 => Hash.Sha256Sha256Ripedm160(PublicKey);

    public string PublicKeyHash => Hash160.ToHexString();
    public string PublicKeyStr => PublicKey.ToHexString();
    public string Wif => new Key(_privateKey.sec.ToBytes()).GetWif(Network.ToNBitcoin()).ToString();

    /// <summary>
    /// Returns signature in DER format
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public byte[] Sign(ReadOnlySpan<byte> data)
    {
        var signature = _privateKey.SignECDSARFC6979(data);
        return signature.ToDER();
    }

    public bool Verify(byte[] der, byte[] data)
        => SecpECDSASignature.TryCreateFromDer(der, out var signature) && _publicKey.SigVerify(signature!, data);

    public static (ECPrivKey, Network?) ParsePrivateKey(string key)
    {
        ECPrivKey privateKey;
        Network? network = null;

        switch (key.Length)
        {
            // WIF
            case 52 or 51:
                {
                    var pKeyWif = Encoders.Base58.DecodeData(key);
                    var privateKeyBytes = pKeyWif[1..33];

                    privateKey = Context.Instance.CreateECPrivKey(privateKeyBytes);
                    network = pKeyWif[0] == 0x80
                        ? Network.Mainnet
                        : pKeyWif[0] == 0xEF
                            ? Network.Testnet
                            : throw new Exception("Unknown WIF format");

                    break;
                }
            // HEX
            case 64:
                privateKey = Context.Instance.CreateECPrivKey(key.FromHexString());

                break;
            default:
                throw new Exception("Unknown something");
        }

        return (privateKey, network);
    }

    public bool Equals(PrivateKey other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Equals(_privateKey, other._privateKey) && Network == other.Network;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;

        return obj.GetType() == GetType() && Equals((PrivateKey)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_privateKey, (int)Network);
    }
}
