using System.Collections.Generic;
using System.Text;

using Dxs.Bsv.Models;
using Dxs.Bsv.Script.Build;
using Dxs.Bsv.Script.Read;

namespace Dxs.Bsv.Extensions;

public static class OutputHelpers
{
    public static string ToAsm(this Output output, Transaction tx, Network network = Network.Mainnet)
    {
        var asm = ToAsm(new OutPoint(tx, output.Idx).ScriptPubKey, network);

        return asm;
    }

    public static string ToAsm(string scriptPubKey, Network network = Network.Mainnet)
    {
        var asm = ToAsm(scriptPubKey.FromHexString(), network);

        return asm;
    }

    private static string ToAsm(IList<byte> scriptPubKey, Network network = Network.Mainnet)
    {
        var scriptBuildTokens = SimpleScriptReader.Read(scriptPubKey, network);

        return GetAsm(scriptBuildTokens);
    }

    internal static string GetAsm(IEnumerable<ScriptBuildToken> tokens)
    {
        var sb = new StringBuilder();

        foreach (var token in tokens)
        {
            if (sb.Length > 0)
                sb.Append(' ');

            var result = token.OpCode is { } opCode
                ? opCode.ToString("g")
                : token.Bytes.ToHexString();

            sb.Append(result);
        }

        return sb.ToString();
    }
}
