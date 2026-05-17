#nullable enable
using System;

using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;

namespace Dxs.Bsv.P2p.Observer;

/// <summary>
/// Wave 2 S1 — pure parsing primitives that extract the watchable
/// properties out of a transaction script without invoking a full
/// script interpreter.
///
/// Three operations:
/// <list type="bullet">
/// <item>
///   <see cref="TryParseP2pkhOutput"/> — match the canonical
///   <c>OP_DUP OP_HASH160 0x14 &lt;20-byte&gt; OP_EQUALVERIFY OP_CHECKSIG</c>
///   locking script and return the 20-byte <c>Hash160</c>.
/// </item>
/// <item>
///   <see cref="TryParseP2pkhInputPubkey"/> — parse a standard
///   P2PKH unlocking script <c>&lt;sig&gt; &lt;pubkey&gt;</c> and
///   compute <c>HASH160(pubkey)</c>. This is the payer hash160 we
///   match against watched addresses on the input side
///   (audit W2 H3 correction — the original draft mistakenly
///   described the locking-script shape here).
/// </item>
/// <item>
///   <see cref="TryParseTokenId"/> — delegate to the existing
///   <see cref="LockingScriptReader"/> + <see cref="ScriptReaderExtensions.GetTokenId"/>
///   for STAS / DSTAS token outputs. Non-token outputs return null.
/// </item>
/// </list>
///
/// All methods are total: malformed / empty / oversized scripts
/// return false with the out parameter at default; no exceptions
/// bubble out of this class.
/// </summary>
public static class TxScriptParser
{
    /// <summary>Canonical P2PKH locking-script size.</summary>
    public const int P2pkhOutputSize = 25;

    /// <summary>P2PKH hash160 length.</summary>
    public const int Hash160Size = 20;

    /// <summary>Compressed secp256k1 pubkey length (BSV/BCH/BTC).</summary>
    public const int CompressedPubkeySize = 33;

    /// <summary>Uncompressed secp256k1 pubkey length.</summary>
    public const int UncompressedPubkeySize = 65;

    /// <summary>
    /// Safety ceiling on inputs we'll try to parse — bigger than any
    /// realistic P2PKH unlocking script. Avoids unbounded iteration on
    /// hostile or oversized scripts.
    /// </summary>
    public const int MaxParseableScriptBytes = 1024;

    /// <summary>
    /// Try to match the canonical P2PKH locking script and extract
    /// the 20-byte <c>Hash160</c>. Returns false for any deviation
    /// from the exact 25-byte shape — non-standard scripts, P2PK
    /// outputs, multisig, op_return data, etc.
    /// </summary>
    public static bool TryParseP2pkhOutput(ReadOnlySpan<byte> script, out ReadOnlySpan<byte> hash160)
    {
        hash160 = default;
        if (script.Length != P2pkhOutputSize) return false;
        if (script[0] != (byte)OpCode.OP_DUP) return false;
        if (script[1] != (byte)OpCode.OP_HASH160) return false;
        if (script[2] != Hash160Size) return false; // push of 20 bytes
        if (script[P2pkhOutputSize - 2] != (byte)OpCode.OP_EQUALVERIFY) return false;
        if (script[P2pkhOutputSize - 1] != (byte)OpCode.OP_CHECKSIG) return false;
        hash160 = script.Slice(3, Hash160Size);
        return true;
    }

    /// <summary>
    /// Try to parse a standard P2PKH unlocking (signature) script
    /// — two pushes, <c>&lt;sig&gt; &lt;pubkey&gt;</c> — and compute
    /// <c>HASH160(pubkey)</c>. Returns false for any other shape
    /// (multisig, custom, single push, push count != 2,
    /// pubkey-not-canonical-size, oversized).
    /// </summary>
    /// <param name="scriptSig">unlocking script bytes</param>
    /// <param name="hash160">on success: 20-byte HASH160(pubkey)</param>
    public static bool TryParseP2pkhInputPubkey(ReadOnlySpan<byte> scriptSig, out byte[]? hash160)
    {
        hash160 = null;
        if (scriptSig.IsEmpty) return false;
        if (scriptSig.Length > MaxParseableScriptBytes) return false;

        // Standard P2PKH unlocking: two pushdata operations.
        // We need to walk exactly two pushes, then expect end-of-script.
        if (!TryReadPush(scriptSig, 0, out var firstPush, out var firstNext)) return false;
        // first push is the signature — ignore the bytes, just advance
        _ = firstPush;
        if (!TryReadPush(scriptSig, firstNext, out var pubkey, out var secondNext)) return false;
        // Anything after the second push → not standard P2PKH input.
        if (secondNext != scriptSig.Length) return false;

        // Canonical pubkey sizes only.
        if (pubkey.Length != CompressedPubkeySize && pubkey.Length != UncompressedPubkeySize) return false;
        // Canonical first-byte marker.
        if (pubkey.Length == CompressedPubkeySize && pubkey[0] != 0x02 && pubkey[0] != 0x03) return false;
        if (pubkey.Length == UncompressedPubkeySize && pubkey[0] != 0x04) return false;

        hash160 = Hash.Sha256Sha256Ripedm160(pubkey);
        return hash160 is { Length: Hash160Size };
    }

    /// <summary>
    /// Try to parse the STAS / DSTAS token-id out of a locking
    /// script. Returns false for non-token scripts. Total — never
    /// throws on malformed input.
    /// </summary>
    public static bool TryParseTokenId(ReadOnlySpan<byte> script, Network network, out string? tokenId)
    {
        tokenId = null;
        if (script.IsEmpty) return false;
        if (script.Length > MaxParseableScriptBytes) return false;
        try
        {
            // ReadOnlySpan<byte> needs to round-trip through a heap
            // array because LockingScriptReader.Read takes byte[].
            // Token parsing is rare relative to P2PKH; allocation
            // here is acceptable.
            var reader = LockingScriptReader.Read(script.ToArray(), network);
            var id = reader.GetTokenId();
            if (string.IsNullOrEmpty(id)) return false;
            tokenId = id;
            return true;
        }
        catch
        {
            // LockingScriptReader is permissive on malformed scripts
            // but we wrap defensively — S1 promise is "no exception
            // bubbles out of this class".
            return false;
        }
    }

    /// <summary>
    /// Read one Bitcoin script push operation starting at
    /// <paramref name="offset"/>. Returns the pushed payload as a
    /// slice and the offset of the next opcode. Supports the
    /// standard push prefixes 0x01..0x4b, OP_PUSHDATA1 (0x4c),
    /// OP_PUSHDATA2 (0x4d), and OP_PUSHDATA4 (0x4e). Returns false
    /// on any malformed encoding (truncated, push beyond script end).
    /// </summary>
    public static bool TryReadPush(
        ReadOnlySpan<byte> script,
        int offset,
        out ReadOnlySpan<byte> payload,
        out int next)
    {
        payload = default;
        next = offset;
        if (offset < 0 || offset >= script.Length) return false;

        var op = script[offset];
        int dataLen;
        int dataStart;

        if (op >= 0x01 && op <= 0x4b)
        {
            // Direct push of `op` bytes.
            dataLen = op;
            dataStart = offset + 1;
        }
        else if (op == 0x4c) // OP_PUSHDATA1
        {
            if (offset + 1 >= script.Length) return false;
            dataLen = script[offset + 1];
            dataStart = offset + 2;
        }
        else if (op == 0x4d) // OP_PUSHDATA2
        {
            if (offset + 2 >= script.Length) return false;
            dataLen = script[offset + 1] | (script[offset + 2] << 8);
            dataStart = offset + 3;
        }
        else if (op == 0x4e) // OP_PUSHDATA4
        {
            if (offset + 4 >= script.Length) return false;
            dataLen = script[offset + 1]
                | (script[offset + 2] << 8)
                | (script[offset + 3] << 16)
                | (script[offset + 4] << 24);
            dataStart = offset + 5;
        }
        else
        {
            // Not a push opcode.
            return false;
        }

        if (dataLen < 0) return false;
        if (dataStart + dataLen > script.Length) return false;
        payload = script.Slice(dataStart, dataLen);
        next = dataStart + dataLen;
        return true;
    }
}
