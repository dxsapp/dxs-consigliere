using System;
using System.Linq;

using Dxs.Bsv.Protocol;
using NBitcoin;

namespace Dxs.Bsv.ScriptEvaluation;

internal static class BsvSignatureHashComputer
{
    public static uint256 Compute(Transaction tx, int inputIndex, byte rawSigHashType, TxOut spentOutput, NBitcoin.Script scriptCode)
    {
        if (inputIndex < 0 || inputIndex >= tx.Inputs.Count)
            throw new ArgumentOutOfRangeException(nameof(inputIndex));

        if (rawSigHashType != 0x41)
            throw new NotSupportedException($"Unsupported repo-native sighash type: 0x{rawSigHashType:x2}.");

        var size = 4 + 32 + 32 + 32 + 4 + BufferWriter.GetChunkSize(scriptCode.ToBytes(true)) + 8 + 4 + 32 + 4 + 4;
        var buffer = new BufferWriter(size);

        buffer.WriteUInt32Le((uint)tx.Version);
        WritePrevoutsHash(buffer, tx);
        WriteSequenceHash(buffer, tx);
        // NBitcoin uint256.ToBytes() already returns the txid in internal little-endian order.
        // The BSV sighash preimage expects little-endian prevout bytes, so do not reverse again.
        buffer.Write(tx.Inputs[inputIndex].PrevOut.Hash.ToBytes());
        buffer.WriteUInt32Le((uint)tx.Inputs[inputIndex].PrevOut.N);
        buffer.WriteChunk(scriptCode.ToBytes(true));
        buffer.WriteUInt64Le((ulong)spentOutput.Value.Satoshi);
        buffer.WriteUInt32Le(tx.Inputs[inputIndex].Sequence);
        WriteOutputsHash(buffer, tx);
        buffer.WriteUInt32Le((uint)tx.LockTime.Value);
        buffer.WriteUInt32Le(rawSigHashType);

        return new uint256(Hash.Sha256Sha256(buffer.Bytes));
    }

    private static void WritePrevoutsHash(BufferWriter buffer, Transaction tx)
    {
        var prevoutsBuffer = new BufferWriter((32 + 4) * tx.Inputs.Count);
        foreach (var input in tx.Inputs)
        {
            prevoutsBuffer.Write(input.PrevOut.Hash.ToBytes());
            prevoutsBuffer.WriteUInt32Le((uint)input.PrevOut.N);
        }

        buffer.Write(Hash.Sha256Sha256(prevoutsBuffer.Bytes));
    }

    private static void WriteSequenceHash(BufferWriter buffer, Transaction tx)
    {
        var sequenceBuffer = new BufferWriter(4 * tx.Inputs.Count);
        foreach (var input in tx.Inputs)
            sequenceBuffer.WriteUInt32Le(input.Sequence);

        buffer.Write(Hash.Sha256Sha256(sequenceBuffer.Bytes));
    }

    private static void WriteOutputsHash(BufferWriter buffer, Transaction tx)
    {
        var size = tx.Outputs.Count * 8 + tx.Outputs.Sum(x => BufferWriter.GetChunkSize(x.ScriptPubKey.ToBytes(true)));
        var outputsBuffer = new BufferWriter(size);

        foreach (var output in tx.Outputs)
        {
            outputsBuffer.WriteUInt64Le((ulong)output.Value.Satoshi);
            outputsBuffer.WriteChunk(output.ScriptPubKey.ToBytes(true));
        }

        buffer.Write(Hash.Sha256Sha256(outputsBuffer.Bytes));
    }
}
