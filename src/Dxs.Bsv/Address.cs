using System;
using Dxs.Bsv.Script;

namespace Dxs.Bsv;

public class Address : IEquatable<Address>
{
    public Address(string address)
    {
        var decoded = BitcoinHelpers.Base58Decode(address);
        if (decoded.Length != 25)
            throw new Exception("Unsupported address");

        Value = address;
        Hash160 = decoded[1..^4];
        Network = BitcoinHelpers.GetByAddressFirstByte(decoded[0]);

        // TODO? verify checksum
    }

    public Address(ReadOnlySpan<byte> hash160, ScriptType scriptType, Network network)
    {
        Value = hash160.GetAddressFromHash160(scriptType, network);
        Hash160 = hash160.ToArray();
        Network = network;
    }

    public string Value { get; }
    public byte[] Hash160 { get; }
    public Network Network { get; }

    public static Address FromPublicKey(ReadOnlySpan<byte> publicKey, ScriptType scriptType, Network network)
    {
        var hash160 = Hash.Sha256Sha256Ripedm160(publicKey);

        return new(hash160, scriptType, network);
    }

    public static bool TryParse(string str, out Address address)
    {
        try
        {
            address = new Address(str);
            return true;
        }
        catch
        {
            address = null;
            return false;
        }
    }

    public bool Equals(string address) => Value.Equals(address, StringComparison.Ordinal);

    public override string ToString() => Value;

    public bool Equals(Address other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;

        return Equals((Address)obj);
    }

    public override int GetHashCode()
    {
        return Value != null ? Value.GetHashCode() : 0;
    }

    public static bool operator ==(Address left, Address right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Address left, Address right)
    {
        return !Equals(left, right);
    }
}
