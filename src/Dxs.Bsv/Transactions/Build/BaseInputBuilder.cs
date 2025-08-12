using System;
using System.Linq;
using Dxs.Bsv.Models;
using Dxs.Bsv.Protocol;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Build;

namespace Dxs.Bsv.Transactions.Build;

public class BaseInputBuilder
{
    /// <param name="txBuilder">Tx this input belongs to</param>
    /// <param name="outPoint">Tx output for this input belongs to </param>
    /// <param name="signer">Private key to sign input</param>
    /// <param name="merge"></param>
    public BaseInputBuilder(TransactionBuilder txBuilder, OutPoint outPoint, PrivateKey signer, bool merge)
    {
        _txBuilder = txBuilder;
        _signer = signer;
        _idx = txBuilder.Inputs.Count;

        Merge = merge;
        OutPoint = outPoint;
    }

    private readonly TransactionBuilder _txBuilder;
    private readonly PrivateKey _signer;
    private readonly int _idx;
    public OutPoint OutPoint { get; }

    public bool Merge { get; }

    /// <summary>
    /// Unlocking script
    /// </summary>
    public byte[] UnlockingScript { get; set; }

    /// <summary>
    /// If input is signed returns actual size otherwise returns max size of signature depends on output type
    /// </summary>
    public int UnlockingScriptSize
    {
        get
        {
            if (UnlockingScript is { Length: > 0 })
                return BufferWriter.GetChunkSize(UnlockingScript.Length);

            // p2pkh signature
            var size = 1 + // OP_PUSH
                       73 + // DER-encoded signature (70-73 bytes)
                       1 + // OP_PUSH
                       33; // Public Key

            if (OutPoint.ScriptType == ScriptType.P2STAS)
            {
                PrepareMergeInfo();

                size += StasNullDataLength;
                size += _txBuilder.Outputs
                    .Where(x => x.Type != ScriptType.NullData)
                    .Sum(x => BufferWriter.GetNumberSize(x.Value) + 21 /*Hash160*/);

                var fundingInput = _txBuilder.Inputs[^1];

                size += BufferWriter.GetNumberSize(fundingInput.OutPoint.Vout);
                size += BufferWriter.GetChunkSize(32);
                size += BufferWriter.GetChunkSize((int)PreimageLength);

                if (Merge)
                {
                    size += BufferWriter.GetNumberSize(_mergeVout);
                    size += _mergeSegments.Sum(BufferWriter.GetChunkSize);
                    size += BufferWriter.GetNumberSize((ulong)_mergeSegments.Length);
                }
                else
                {
                    size += 1; // OP_0
                }
            }

            return BufferWriter.GetChunkSize(size);
        }
    }

    public uint Sequence => TransactionBuilder.DefaultSequence;

    private int PrevoutHashLength => (32 + 4) * _txBuilder.Inputs.Count;

    private int StasNullDataLength
    {
        get
        {
            var nullDataOutputs = _txBuilder.Outputs
                .Where(x => x.Type == ScriptType.NullData)
                .ToArray();

            if (!nullDataOutputs.Any())
                return 1;

            return BufferWriter.GetChunkSize(nullDataOutputs.First().LockingScript[2..]);
        }
    }

    public ulong PreimageLength =>
        4 + // Tx version
        32 + // Prevout hash
        32 + // Sequence hash
        32 + // Output Tx id
        4 + // VOUT ;
        (ulong)BufferWriter.GetChunkSize(OutPoint.ScriptPubKey) +
        8 + // Satoshis
        4 + // Sequence
        32 + //Outputs hash
        4 + // Lock time
        4; // Signature type

    public ulong Size =>
        32 + // TX.Id
        4 + // Vout
        (ulong)UnlockingScriptSize +
        4; // Sequence

    public void WriteToBuffer(BufferWriter buffer)
    {
        buffer.WriteReverse(OutPoint.TransactionId.FromHexString());
        buffer.WriteUInt32Le(OutPoint.Vout);
        buffer.WriteChunk(UnlockingScript);
        buffer.WriteUInt32Le(Sequence);
    }

    /// <summary>
    /// Only SIGHASH_ALL|FORK_ID implemented
    /// </summary>
    public BufferWriter Preimage(SignatureHashType signatureHashType)
    {
        var size = PreimageLength;
        var preimageBuffer = new BufferWriter((int)size);

        preimageBuffer.WriteUInt32Le(_txBuilder.Version); // 4
        WritePrevoutHash(preimageBuffer); // 32
        WriteSequenceHash(preimageBuffer); // 32
        preimageBuffer.WriteReverse(OutPoint.TransactionId.FromHexString()); // 32
        preimageBuffer.WriteUInt32Le(OutPoint.Vout); // 4
        preimageBuffer.WriteChunk(OutPoint.ScriptPubKey);
        preimageBuffer.WriteUInt64Le(OutPoint.Satoshis); // 8
        preimageBuffer.WriteUInt32Le(Sequence); // 4
        WriteOutputsHash(preimageBuffer); // 32
        preimageBuffer.WriteUInt32Le(_txBuilder.LockTime); // 4
        preimageBuffer.WriteUInt32Le((uint)signatureHashType); // 4

        return preimageBuffer;
    }

    public void Sign()
    {
        var preimage = Preimage(TransactionBuilder.DefaultSighashType);
        var sighash = Hash.Sha256Sha256(preimage.Bytes);
        var der = _signer.Sign(sighash);

        var derWithSighash = new byte[der.Length + 1];
        for (var i = 0; i < der.Length; i++)
            derWithSighash[i] = der[i];

        derWithSighash[^1] = (byte)TransactionBuilder.DefaultSighashType;

        switch (OutPoint.ScriptType)
        {
            case ScriptType.P2PKH:
            {
                var size = BufferWriter.GetChunkSize(derWithSighash) + BufferWriter.GetChunkSize(_signer.PublicKey);
                var buffer = new BufferWriter(size);

                buffer.WriteChunk(derWithSighash);
                buffer.WriteChunk(_signer.PublicKey);

                UnlockingScript = buffer.Bytes;
                break;
            }
            case ScriptType.P2STAS:
            {
                PrepareMergeInfo();

                var script = new ScriptBuilder(ScriptType.P2STAS, null);
                var hasNote = false;

                foreach (var output in _txBuilder.Outputs)
                {
                    if (output.Type == ScriptType.NullData)
                    {
                        script.AddData(output.LockingScript[2..]);
                        hasNote = true;
                    }
                    else
                    {
                        script
                            .AddNumber(output.Value)
                            .AddData(output.Address.Hash160);
                    }
                }

                if (!hasNote)
                    script.AddOpCode(OpCode.OP_0);

                var fundingInput = _txBuilder.Inputs[^1];

                script
                    .AddNumber(fundingInput.OutPoint.Vout)
                    .AddData(fundingInput.OutPoint.TransactionId.FromHexString().Reverse());

                if (Merge)
                {
                    script
                        .AddNumber(_mergeVout)
                        .AddData(_mergeSegments)
                        .AddNumber((ulong)_mergeSegments.Length);
                }
                else
                {
                    script
                        .AddOpCode(OpCode.OP_0);
                }

                script.AddData(preimage.Bytes);
                script.AddData(derWithSighash.ToArray());
                script.AddData(_signer.PublicKey);

                UnlockingScript = script.Bytes;
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void WritePrevoutHash(BufferWriter bufferWriter)
    {
        var prevoutsBuffer = new BufferWriter(PrevoutHashLength);

        foreach (var input in _txBuilder.Inputs)
        {
            prevoutsBuffer.WriteReverse(input.OutPoint.TransactionId.FromHexString());
            prevoutsBuffer.WriteUInt32Le(input.OutPoint.Vout);
        }

        Span<byte> hash = stackalloc byte[32];
        Hash.Sha256Sha256(prevoutsBuffer.Bytes, hash);

        bufferWriter.Write(hash.ToArray());
    }

    private void WriteSequenceHash(BufferWriter buffer)
    {
        var buf = new BufferWriter(4 * _txBuilder.Inputs.Count);

        foreach (var input in _txBuilder.Inputs)
        {
            buf.WriteUInt32Le(input.Sequence);
        }

        Span<byte> hash = stackalloc byte[32];
        Hash.Sha256Sha256(buf.Bytes, hash);

        buffer.Write(hash.ToArray());
    }

    private void WriteOutputsHash(BufferWriter buffer)
    {
        var size = _txBuilder.Outputs.Count * 8 +
                   _txBuilder.Outputs.Select(x => BufferWriter.GetChunkSize(x.LockingScript)).Sum();
        var buf = new BufferWriter(size);

        foreach (var output in _txBuilder.Outputs)
        {
            buf.WriteUInt64Le(output.Value);
            buf.WriteChunk(output.LockingScript);
        }

        Span<byte> hash = stackalloc byte[32];
        Hash.Sha256Sha256(buf.Bytes, hash);

        buffer.Write(hash.ToArray());
    }

    private ulong _mergeVout;
    private byte[][] _mergeSegments;

    private void PrepareMergeInfo()
    {
        if (!Merge || _mergeSegments != null)
            return;

        var lockingScript = _txBuilder.Inputs[0].OutPoint.ScriptPubKey;
        var scriptToCut = lockingScript[23..];
        var mergeUtxo = _txBuilder.Inputs[_idx == 0 ? 1 : 0];

        _mergeVout = mergeUtxo.OutPoint.Vout;
        _mergeSegments = BinaryHelpers.Split(mergeUtxo.OutPoint.Transaction.Raw, scriptToCut).Reverse().ToArray();
    }
}