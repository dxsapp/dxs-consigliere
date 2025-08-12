using System;
using System.Collections.Generic;
using System.Linq;
using Dxs.Bsv.Models;
using Dxs.Bsv.Protocol;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Build;
using Dxs.Bsv.Script.Read;
using Dxs.Bsv.Tokens;

namespace Dxs.Bsv.Transactions.Build;

public class TransactionBuilder
{
    public const uint DefaultSequence = uint.MaxValue;
    public const SignatureHashType DefaultSighashType = SignatureHashType.SIGHASH_ALL | SignatureHashType.SIGHASH_FORKID;

    private TransactionBuilder() { }

    private readonly List<BaseInputBuilder> _inputs = new();
    private readonly List<OutputBuilder> _outputs = new();

    public string Id => BitcoinHelpers.GetTxId(ToBytes());
    public string Hex => ToBytes().ToHexString();

    public uint Version => 1;
    public uint LockTime => 0;
    public IReadOnlyList<BaseInputBuilder> Inputs => _inputs;
    public IReadOnlyList<OutputBuilder> Outputs => _outputs;

    public ulong EstimatedFee { get; private set; }

    public long Size =>
        4L + // version
        4L + // locktime
        BufferWriter.GetVarIntLength(_inputs.Count) +
        _inputs.Sum(x => (int)x.Size) +
        BufferWriter.GetVarIntLength(_outputs.Count) +
        _outputs.Sum(x => (int)x.Size);

    public long SizeForFee;

    public static TransactionBuilder Init() => new();

    /// <summary>
    /// For signed transaction returns actual size,
    /// For not signed transaction returns approximate size with max input size
    /// </summary>
    public ulong GetFee(decimal satoshisPerByte) => (ulong)Math.Round(Size * satoshisPerByte, MidpointRounding.ToPositiveInfinity);

    public Transaction BuildTransaction(Network network)
        => Transaction.Parse(ToBytes(), network);

    public Transaction SignAndBuildTransaction(Network network)
    {
        Sign();

        return BuildTransaction(network);
    }

    public TransactionBuilder AddInput(OutPoint outPoint, PrivateKey signer)
    {
        _inputs.Add(new BaseInputBuilder(this, outPoint, signer, false));

        return this;
    }

    public TransactionBuilder AddInputs(IList<OutPoint> outPoints, PrivateKey signer)
    {
        foreach (var outPoint in outPoints)
        {
            _inputs.Add(new BaseInputBuilder(this, outPoint, signer, false));
        }

        return this;
    }

    public TransactionBuilder AddStasMergeInput(OutPoint outPoint, PrivateKey signer)
    {
        _inputs.Add(new BaseInputBuilder(this, outPoint, signer, true));

        return this;
    }

    public TransactionBuilder AddP2PkhOutput(ulong value, Address toAddress, params byte[][] data)
    {
        var script = new P2PkhBuilderScript(toAddress);

        foreach (var d in data.Where(x => x?.Length > 0))
            script.AddReturnData(d);

        _outputs.Add(new OutputBuilder
        {
            Value = value,
            LockingScript = script.Bytes,
            Address = toAddress,
            Type = ScriptType.P2PKH
        });

        return this;
    }

    public TransactionBuilder AddNullDataOutput(params byte[][] data)
    {
        var script = new NullDataScriptBuilder(data);

        _outputs.Add(new OutputBuilder
        {
            Value = 0,
            LockingScript = script.Bytes,
            Type = ScriptType.NullData
        });

        return this;
    }

    public TransactionBuilder AddStasOutput(ulong value, ITokenSchema schema, Address toAddress, params byte[][] data)
    {
        var script = new P2StasScriptBuilder(toAddress, schema);

        data = data?.Where(x => x?.Length > 0).ToArray();
        if (data != null)
            foreach (var d in data)
            {
                script.AddData(d);
            }

        _outputs.Add(new OutputBuilder
        {
            Value = value,
            LockingScript = script.Bytes,
            Address = toAddress,
            Type = ScriptType.P2STAS
        });

        return this;
    }

    public TransactionBuilder AddStasOutput(ulong value, Address toAddress, IList<byte> stasPubKey, params byte[][] data)
    {
        var scriptBytes = new byte[stasPubKey.Count];

        for (var i = 0; i < stasPubKey.Count; i++)
        {
            if (i is >= 3 and <= 22)
                scriptBytes[i] = toAddress.Hash160[i - 3];
            else
                scriptBytes[i] = stasPubKey[i];
        }

        data = data?.Where(x => x?.Length > 0).ToArray();
        if (data?.Length > 0)
        {
            var scriptTokens = SimpleScriptReader.Read(scriptBytes, toAddress.Network);
            var builder = new ScriptBuilder(ScriptType.P2STAS, toAddress);

            foreach (var token in scriptTokens)
            {
                builder.AddToken(token.Clone());
            }

            foreach (var d in data)
                builder.AddData(d);

            scriptBytes = builder.Bytes;
        }

        _outputs.Add(new OutputBuilder
        {
            Value = value,
            LockingScript = scriptBytes.ToArray(),
            Address = toAddress,
            Type = ScriptType.P2STAS
        });

        return this;
    }

    public TransactionBuilder AddChangeOutputWithFee(
        Address toAddress,
        ulong change,
        decimal satoshisPerByte,
        int? idx = null
    )
    {
        var script = new P2PkhBuilderScript(toAddress);
        var output = new OutputBuilder
        {
            Value = change,
            LockingScript = script.Bytes,
            Address = toAddress,
            Type = ScriptType.P2PKH
        };

        if (idx is { } i)
            _outputs.Insert(i, output);
        else
            _outputs.Add(output);

        var fee = GetFee(satoshisPerByte);

        if (fee >= change)
            throw new Exception($"Insufficient satoshis to pay fee: Fee:{fee} > Change:{change}");

        output.Value = change - fee;
        EstimatedFee = fee;
        SizeForFee = Size;

        return this;
    }

    public TransactionBuilder Sign()
    {
        foreach (var input in _inputs)
            input.Sign();

        return this;
    }

    public byte[] ToBytes()
    {
        var buffer = new BufferWriter(Size);

        buffer.WriteUInt32Le(Version);
        buffer.WriteVarInt((ulong)_inputs.Count);

        foreach (var input in _inputs)
            input.WriteToBuffer(buffer);

        buffer.WriteVarInt((ulong)_outputs.Count);
        foreach (var output in _outputs)
            output.WriteToBuffer(buffer);

        buffer.WriteUInt32Le(LockTime);

        return buffer.Bytes;
    }

    private void WriteToBuffer() { }
}