using System.Collections.Generic;
using System.IO;

using Dxs.Bsv.Protocol;
using Dxs.Bsv.Script.Build;

namespace Dxs.Bsv.Script.Read;

public class UnlockingScriptReader(
    BitcoinStreamReader bitcoinStreamReader,
    int length,
    Network network
) : BaseScriptReader(bitcoinStreamReader, length, network)
{
    private readonly List<ScriptBuildToken> _tokens = [];

    // Returns valid address for P2PKH and STAS unlocking script, cannot guarantee validity for other types
    public Address Address { get; private set; }
    public int? DstasSpendingType { get; private set; }

    public IReadOnlyList<ScriptBuildToken> Tokens => _tokens;

    protected override bool HandleToken(ScriptReadToken token, int tokenIdx, bool isLastToken)
    {
        _tokens.Add(new ScriptBuildToken(token));

        if (isLastToken && token.Bytes.Length == 33 && token.Bytes[0] is 2 or 3)
        {
            try
            {
                Address = Address.FromPublicKey(token.Bytes, ScriptType.P2PKH, Network);
            }
            catch { }
        }

        return true;
    }

    public static UnlockingScriptReader Read(string hex, Network network)
    {
        var bytes = hex.FromHexString();

        return Read(bytes, network);
    }

    public static UnlockingScriptReader Read(byte[] bytes, Network network)
    {
        using var stream = new MemoryStream(bytes);

        return Read(stream, network);
    }

    public static UnlockingScriptReader Read(Stream stream, Network network)
    {
        using var bitcoinStreamReader = new BitcoinStreamReader(stream);

        return Read(bitcoinStreamReader, (int)stream.Length, network);
    }

    public static UnlockingScriptReader Read(BitcoinStreamReader bitcoinStreamReader, int expectedLength, Network network)
    {
        var reader = new UnlockingScriptReader(bitcoinStreamReader, expectedLength, network);
        reader.ReadInternal();
        reader.TryParseDstasSpendingType();

        return reader;
    }

    private void TryParseDstasSpendingType()
    {
        if (_tokens.Count < 3)
            return;

        for (var preimageIdx = _tokens.Count - 2; preimageIdx >= 0; preimageIdx--)
        {
            var preimageToken = _tokens[preimageIdx];
            if (!LooksLikeDstasPreimage(preimageToken))
                continue;

            var spendingTypeIdx = preimageIdx + 1;
            if (spendingTypeIdx >= _tokens.Count)
                continue;

            var spendingTypeToken = _tokens[spendingTypeIdx];
            if (!TryParseScriptNumber(spendingTypeToken, out var spendingType) || !IsRecognizedDstasSpendingType(spendingType))
                continue;

            if (!LooksLikeSimpleDstasTail(spendingTypeIdx) && !LooksLikeMultisigDstasTail(spendingTypeIdx))
                continue;

            DstasSpendingType = spendingType;
            return;
        }
    }

    private bool LooksLikeSimpleDstasTail(int spendingTypeIdx)
    {
        if (spendingTypeIdx + 2 >= _tokens.Count)
            return false;

        var sigToken = _tokens[spendingTypeIdx + 1];
        var pubKeyToken = _tokens[spendingTypeIdx + 2];

        return LooksLikeSignature(sigToken) && LooksLikeCompressedPublicKey(pubKeyToken);
    }

    private bool LooksLikeMultisigDstasTail(int spendingTypeIdx)
    {
        if (spendingTypeIdx + 3 >= _tokens.Count)
            return false;

        var dummyToken = _tokens[spendingTypeIdx + 1];
        if (dummyToken.OpCodeNum != (byte)OpCode.OP_0 || dummyToken.Bytes.Length != 0)
            return false;

        var authorityPreimageToken = _tokens[^1];
        if (!LooksLikeMpkhPreimage(authorityPreimageToken))
            return false;

        for (var i = spendingTypeIdx + 2; i < _tokens.Count - 1; i++)
        {
            if (!LooksLikeSignature(_tokens[i]))
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
        => spendingType is >= 0 and <= 4;

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
