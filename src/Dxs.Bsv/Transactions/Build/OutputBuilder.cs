using Dxs.Bsv.Protocol;
using Dxs.Bsv.Script;

namespace Dxs.Bsv.Transactions.Build;

public class OutputBuilder
{
    public ulong Value { get; set; }

    /// <summary>
    /// Locking script
    /// </summary>
    public byte[] LockingScript { get; init; }

    public Address Address { get; set; }

    public ScriptType Type { get; set; }

    public ulong Size =>
        8 + // Value
        (ulong)BufferWriter.GetChunkSize(LockingScript);

    public void WriteToBuffer(BufferWriter buffer)
    {
        buffer.WriteUInt64Le(Value);
        buffer.WriteChunk(LockingScript);
    }
}