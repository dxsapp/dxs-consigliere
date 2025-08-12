using System;
using Dxs.Bsv.Script.Read;

namespace Dxs.Bsv.Script.Build;

public class ScriptBuildToken
{
    private ScriptBuildToken() { }

    public ScriptBuildToken(OpCode opCode)
    {
        OpCodeNum = (byte)opCode;
        OpCode = opCode;
    }

    public ScriptBuildToken(byte[] data)
    {
        DataLength = data.Length;
        Bytes = data;

        OpCodeNum = (ulong)data.Length switch
        {
            0 => throw new Exception("No data provided"),
            < 76 => (byte)data.Length,
            <= byte.MaxValue => (byte)Dxs.Bsv.Script.OpCode.OP_PUSHDATA1,
            <= ushort.MaxValue => (byte)Dxs.Bsv.Script.OpCode.OP_PUSHDATA2,
            <= uint.MaxValue => (byte)Dxs.Bsv.Script.OpCode.OP_PUSHDATA4,
            _ => throw new Exception("Too much")
        };
    }

    public ScriptBuildToken(byte opCodeNum, int dataLength = 0, bool isReceiverId = false)
    {
        OpCodeNum = opCodeNum;
        DataLength = dataLength;
        IsReceiverId = isReceiverId;

        if (OpCodeNum.IsOpCode(out var opCode))
            OpCode = opCode;
        else
            OpCode = null;
    }

    public ScriptBuildToken(ScriptReadToken token) : this(token.OpCodeNum, token.Bytes.Length)
    {
        Bytes = token.Bytes.ToArray();
    }

    public ScriptBuildToken(ScriptBuildToken token)
    {
        OpCodeNum = token.OpCodeNum;
        DataLength = token.DataLength;
        IsReceiverId = token.IsReceiverId;
        OpCode = token.OpCode;
        Bytes = token.Bytes;
    }

    public byte OpCodeNum { get; }
    public int DataLength { get; }
    public bool IsReceiverId { get; set; }
    public OpCode? OpCode { get; }
    public byte[] Bytes { get; private set; }
    public bool UnknownLengthData { get; private init; }

    public bool Same(ScriptReadToken token) =>
        UnknownLengthData
            ? token.Bytes.Length > 0
            : OpCodeNum == token.OpCodeNum && DataLength == token.Bytes.Length;

    public override string ToString()
    {
        if (Bytes is not null &&
            Bytes.Length == 0 && DataLength > 0)
        {
            Bytes = new byte[DataLength];
            Array.Fill(Bytes, (byte)0);
        }

        if (OpCode is { } opCode && DataLength > 0)
            return $"{opCode:G} {Bytes.ToHexString()}";

        if (DataLength == 0)
            return $"{OpCode:G}";

        return Bytes?.ToHexString();
    }

    public ScriptBuildToken Clone() => new(this);

    public static ScriptBuildToken UnknownLengthDataToken => new()
    {
        UnknownLengthData = true,
    };
}