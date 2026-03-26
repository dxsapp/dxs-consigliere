#nullable enable
using System.Collections.Generic;

using Dxs.Bsv.Protocol;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Build;
using Dxs.Bsv.Script.Read;
using Dxs.Bsv.Tokens.Dstas.Models;

namespace Dxs.Bsv.Tokens.Dstas.Parsing;

public static class DstasUnlockingScriptParser
{
    public static DstasUnlockingSemantics? Parse(UnlockingScriptReader reader)
        => reader is null
            ? null
            : TryParse(reader.Tokens, out var semantics)
                ? semantics
                : null;

    public static bool TryParse(IReadOnlyList<ScriptBuildToken> tokens, out DstasUnlockingSemantics? semantics)
    {
        semantics = null;

        if (tokens.Count < 3)
            return false;

        for (var preimageIdx = tokens.Count - 2; preimageIdx >= 0; preimageIdx--)
        {
            var preimageToken = tokens[preimageIdx];
            if (!LooksLikeDstasPreimage(preimageToken))
                continue;

            var spendingTypeIdx = preimageIdx + 1;
            if (spendingTypeIdx >= tokens.Count)
                continue;

            var spendingTypeToken = tokens[spendingTypeIdx];
            if (!TryParseScriptNumber(spendingTypeToken, out var spendingType) || !IsRecognizedDstasSpendingType(spendingType))
                continue;

            var usesSimpleTail = LooksLikeSimpleDstasTail(tokens, spendingTypeIdx);
            var usesAuthorityTail = LooksLikeMultisigDstasTail(tokens, spendingTypeIdx);
            if (!usesSimpleTail && !usesAuthorityTail)
                continue;

            semantics = new DstasUnlockingSemantics(spendingType, usesSimpleTail, usesAuthorityTail);
            return true;
        }

        return false;
    }

    private static bool LooksLikeSimpleDstasTail(IReadOnlyList<ScriptBuildToken> tokens, int spendingTypeIdx)
    {
        if (spendingTypeIdx + 2 >= tokens.Count)
            return false;

        var sigToken = tokens[spendingTypeIdx + 1];
        var pubKeyToken = tokens[spendingTypeIdx + 2];

        return LooksLikeSignature(sigToken) && LooksLikeCompressedPublicKey(pubKeyToken);
    }

    private static bool LooksLikeMultisigDstasTail(IReadOnlyList<ScriptBuildToken> tokens, int spendingTypeIdx)
    {
        if (spendingTypeIdx + 3 >= tokens.Count)
            return false;

        var dummyToken = tokens[spendingTypeIdx + 1];
        if (dummyToken.OpCodeNum != (byte)OpCode.OP_0 || dummyToken.Bytes.Length != 0)
            return false;

        var authorityPreimageToken = tokens[^1];
        if (!LooksLikeMpkhPreimage(authorityPreimageToken))
            return false;

        for (var i = spendingTypeIdx + 2; i < tokens.Count - 1; i++)
        {
            if (!LooksLikeSignature(tokens[i]))
                return false;
        }

        return true;
    }

    private static bool LooksLikeDstasPreimage(ScriptBuildToken token)
        => token.Bytes is { Length: >= 120 };

    private static bool LooksLikeSignature(ScriptBuildToken token)
        => token.Bytes is { Length: > 8 };

    private static bool LooksLikeCompressedPublicKey(ScriptBuildToken token)
        => token.Bytes is { Length: 33 } && token.Bytes[0] is 2 or 3;

    private static bool LooksLikeMpkhPreimage(ScriptBuildToken token)
        => token.Bytes is { Length: >= 35 };

    private static bool IsRecognizedDstasSpendingType(int spendingType)
        => spendingType is >= DstasSpendingTypes.IssueOrNone and <= DstasSpendingTypes.SwapCancel;

    private static bool TryParseScriptNumber(ScriptBuildToken token, out int number)
    {
        number = default;

        if (token.Bytes is { Length: > 0 })
            return TryParseScriptNumber(token.Bytes, out number);

        var opCodeNum = token.OpCodeNum;
        if (opCodeNum == (byte)OpCode.OP_0)
        {
            number = 0;
            return true;
        }

        if (opCodeNum == (byte)OpCode.OP_1NEGATE)
        {
            number = -1;
            return true;
        }

        if (opCodeNum >= (byte)OpCode.OP_1 && opCodeNum <= (byte)OpCode.OP_16)
        {
            number = opCodeNum - (byte)OpCode.OP_1 + 1;
            return true;
        }

        return false;
    }

    private static bool TryParseScriptNumber(IReadOnlyList<byte> bytes, out int number)
    {
        number = default;

        if (bytes.Count == 0)
        {
            number = 0;
            return true;
        }

        if (bytes.Count > 4)
            return false;

        var value = 0;
        for (var i = 0; i < bytes.Count; i++)
            value |= bytes[i] << (8 * i);

        var last = bytes[^1];
        var isNegative = (last & 0x80) != 0;
        if (isNegative)
        {
            value &= ~(0x80 << (8 * (bytes.Count - 1)));
            value = -value;
        }

        number = value;
        return true;
    }
}
