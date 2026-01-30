using System;

using Destructurama.Attributed;

using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Build;

namespace Dxs.Bsv.Models;

public readonly struct OutPoint : IEquatable<OutPoint>
{
    public OutPoint() { }

    public OutPoint(Transaction transaction, ulong vout)
    {
        TransactionId = transaction.Id;
        Transaction = transaction;
        Vout = (uint)vout;

        var output = transaction.Outputs[(int)Vout];

        ScriptPubKey = new byte[output.ScriptPubKey.Length];
        Satoshis = output.Satoshis;
        Address = output.Address;
        TokenId = output.TokenId;
        ScriptType = output.Type;

        Array.Copy(
            transaction.Raw,
            output.ScriptPubKey.Start,
            ScriptPubKey,
            0,
            output.ScriptPubKey.Length
        );
    }

    public OutPoint(
        string transactionId,
        Address address,
        string tokenId,
        ulong satoshis,
        uint vout,
        string scriptPubKeyHex,
        ScriptType scriptType
    )
    {
        TransactionId = transactionId;
        Transaction = null;
        Vout = vout;
        ScriptPubKey = scriptPubKeyHex.FromHexString();
        Satoshis = satoshis;
        Address = address;
        TokenId = tokenId;
        ScriptType = scriptType;
    }

    /// <summary>
    /// Builds outpoint with DEFAULT p2pkh locking script,
    /// if using Utxo was locked by p2pkp script with op_return data, signature
    /// verification will fail
    /// </summary>
    public OutPoint(string transactionId, Address address, string tokenId, ulong satoshis, uint vout)
    {
        TransactionId = transactionId;
        Transaction = null;
        Vout = vout;
        ScriptPubKey = new P2PkhBuilderScript(address).Bytes;
        Satoshis = satoshis;
        Address = address;
        TokenId = tokenId;
        ScriptType = ScriptType.P2PKH;
    }

    public string TransactionId { get; }

    [LogAsScalar]
    public Transaction Transaction { get; }

    public uint Vout { get; }
    public ulong Satoshis { get; }
    public Address Address { get; }
    public string TokenId { get; }
    public byte[] ScriptPubKey { get; }
    public ScriptType ScriptType { get; }

    public bool IsDefault => default == this;

    public override string ToString() => $"{TransactionId}:{Vout}";

    public bool Equals(OutPoint other)
    {
        return TransactionId == other.TransactionId && Vout == other.Vout;
    }

    public override bool Equals(object obj)
    {
        return obj is OutPoint other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TransactionId, Vout);
    }

    public static bool operator ==(OutPoint left, OutPoint right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(OutPoint left, OutPoint right)
    {
        return !left.Equals(right);
    }
}
