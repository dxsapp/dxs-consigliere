namespace Dxs.Bsv.Script;

public static class OpCodeHelpers
{
    public static bool IsOpCode(this byte opCodeNum, out OpCode opCode)
    {
        if (opCodeNum is (byte)OpCode.OP_0 or >= (byte)OpCode.OP_PUSHDATA1 and <= (byte)OpCode.OP_INVALIDOPCODE)
        {
            opCode = (OpCode)opCodeNum;
            return true;
        }

        opCode = OpCode.OP_INVALIDOPCODE;

        return false;
    }
}