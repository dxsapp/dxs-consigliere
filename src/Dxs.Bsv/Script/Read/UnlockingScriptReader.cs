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

        return reader;
    }
}