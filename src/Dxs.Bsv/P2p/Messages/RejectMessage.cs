#nullable enable
using System;

using Dxs.Bsv.P2p.Codec;

namespace Dxs.Bsv.P2p.Messages;

/// <summary>
/// Reject code values per BSV <c>protocol.h::RejectCode</c>.
/// </summary>
public enum RejectCode : byte
{
    Malformed       = 0x01,
    Invalid         = 0x10,
    Obsolete        = 0x11,
    Duplicate       = 0x12,
    NonStandard     = 0x40,
    Dust            = 0x41,
    InsufficientFee = 0x42,
    Checkpoint      = 0x43,
    StreamSetup     = 0x50,
}

/// <summary>
/// Classification of a reject reason into broad categories used by the
/// outgoing-transaction state machine. See design doc §5.2.
/// </summary>
public enum RejectClass
{
    /// <summary>Operator/policy-level rejection (low fee, dust, non-standard).</summary>
    PolicyRejected,
    /// <summary>Tx is fundamentally invalid (bad script, malformed).</summary>
    Invalid,
    /// <summary>Tx conflicts with mempool or spends already-spent inputs.</summary>
    Conflicted,
    /// <summary>Mempool full / already-known — peer-side transient.</summary>
    Transient,
    /// <summary>Reason not recognised; treat as unknown.</summary>
    Unknown,
}

/// <summary>
/// <c>reject</c>: peer rejects a previously-sent message. For our outgoing
/// transactions, the payload carries the offending txid in <see cref="Hash"/>.
/// Wire format (net_processing.cpp:1475-1497):
/// var_str message + 1-byte code + var_str reason + (optional) 32-byte hash.
/// </summary>
public sealed record RejectMessage(string Message, RejectCode Code, string Reason, byte[]? Hash)
{
    public const int MaxMessageLength = 12;
    public const int MaxReasonLength  = 111;

    /// <summary>
    /// Classify the reject reason against the table in design doc §5.2.
    /// </summary>
    public RejectClass Classify()
    {
        var r = Reason.ToLowerInvariant();
        if (r.Contains("missing-inputs") || r.Contains("txn-mempool-conflict") || r.Contains("inputs-missingorspent"))
            return RejectClass.Conflicted;
        if (r.Contains("mandatory-script-verify-flag-failed") || r.StartsWith("bad-txns-"))
            return RejectClass.Invalid;
        if (r.Contains("dust") || r.Contains("min-fee") || r.Contains("min relay fee") || r.Contains("too-small"))
            return RejectClass.PolicyRejected;
        if (r.Contains("mempool-full") || r.Contains("txn-already-known") || r.Contains("already known"))
            return RejectClass.Transient;
        if (r.Contains("insufficient-fee") || r.Contains("insufficient fee"))
            return RejectClass.PolicyRejected;
        return RejectClass.Unknown;
    }

    public byte[] Serialize()
    {
        var w = new P2pWriter();
        w.WriteVarStr(Message);
        w.WriteByte((byte)Code);
        w.WriteVarStr(Reason);
        if (Hash is not null)
        {
            if (Hash.Length != 32) throw new InvalidOperationException("Reject hash must be 32 bytes");
            w.WriteBytes(Hash);
        }
        return w.ToArray();
    }

    public static RejectMessage Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new P2pReader(payload);
        var message = reader.ReadVarStr(MaxMessageLength, "reject.message");
        var code = (RejectCode)reader.ReadByte("reject.code");
        var reason = reader.ReadVarStr(MaxReasonLength, "reject.reason");
        byte[]? hash = null;
        if (reader.Remaining >= 32)
            hash = reader.ReadBytes(32, "reject.hash").ToArray();
        return new RejectMessage(message, code, reason, hash);
    }
}
