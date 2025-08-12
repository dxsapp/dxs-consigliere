using System;

namespace Dxs.Bsv.Script.Read;

public readonly ref struct ScriptReadToken
{
    public ScriptReadToken(byte opCodeNum, ReadOnlySpan<byte> bytes)
    {
        OpCodeNum = opCodeNum;

        if (OpCodeNum.IsOpCode(out var opCode))
            OpCode = opCode;
        else
            OpCode = null;

        Bytes = bytes;
    }

    public byte OpCodeNum { get; }

    public ReadOnlySpan<byte> Bytes { get; }

    public OpCode? OpCode { get; }
}