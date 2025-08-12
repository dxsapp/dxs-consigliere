using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dxs.Bsv.Extensions;
using Dxs.Bsv.Protocol;
using Dxs.Bsv.Script.Read;

namespace Dxs.Bsv.Script.Build;

public class ScriptBuilder(ScriptType scriptType, Address toAddress)
{
    protected readonly List<ScriptBuildToken> Tokens = new();

    protected Address ToAddress { get; } = toAddress;

    public int Size => Tokens.Sum(GetScriptTokenSize);

    public ScriptType ScriptType { get; } = scriptType;

    public byte[] Bytes
    {
        get
        {
            var writer = new BufferWriter(Size);
            foreach (var token in Tokens)
            {
                var tokenRef = new ScriptReadToken(token.OpCodeNum, token.Bytes);
                writer.WriteScriptToken(tokenRef);
            }

            return writer.Bytes;
        }
    }

    public ScriptBuilder AddToken(ScriptBuildToken token)
    {
        Tokens.Add(token);

        return this;
    }

    public ScriptBuilder AddOpCode(OpCode opCode)
    {
        Tokens.Add(new ScriptBuildToken(opCode));

        return this;
    }

    public ScriptBuilder AddData(byte[] data)
    {
        Tokens.Add(new ScriptBuildToken(data));

        return this;
    }

    public ScriptBuilder AddData(byte[][] data)
    {
        foreach (var chunk in data)
            Tokens.Add(new ScriptBuildToken(chunk));

        return this;
    }

    public ScriptBuilder AddData(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        Tokens.Add(new ScriptBuildToken(bytes));

        return this;
    }

    public ScriptBuilder AddNumber(ulong data)
    {
        if (data == 0)
            AddOpCode(OpCode.OP_0);
        else if (data <= 16)
            AddOpCode((OpCode)(0x50 + data));
        else
            AddData(BufferWriter.GetNumberBufferLe((long)data));

        return this;
    }

    public string ToAsm()
    {
        return OutputHelpers.GetAsm(Tokens);
    }

    public static int GetScriptTokenSize(ScriptBuildToken token)
    {
        var size = 1;

        var opcodeNum = token.OpCodeNum;
        var dataLength = token.DataLength;

        size += opcodeNum switch
        {
            > 0 and < (byte)OpCode.OP_PUSHDATA1 => dataLength,
            (byte)OpCode.OP_PUSHDATA1 => dataLength + 1,
            (byte)OpCode.OP_PUSHDATA2 => dataLength + 2,
            (byte)OpCode.OP_PUSHDATA4 => dataLength + 4,
            _ => 0
        };

        return size;
    }
}