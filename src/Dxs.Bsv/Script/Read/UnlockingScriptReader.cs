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

        var pubKeyToken = _tokens[^1];
        var sigToken = _tokens[^2];
        var spendingTypeToken = _tokens[^3];

        // Heuristic: DSTAS-like unlocking scripts end with pubkey + signature + spending-type.
        if (pubKeyToken.Bytes is not { Length: 33 } || sigToken.Bytes is not { Length: > 8 })
            return;

        if (TryParseScriptNumber(spendingTypeToken, out var spendingType))
            DstasSpendingType = spendingType;
    }

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
