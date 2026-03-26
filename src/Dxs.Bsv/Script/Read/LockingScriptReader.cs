using System;
using System.Collections.Generic;
using System.IO;

using Dxs.Bsv.Protocol;
using Dxs.Bsv.Tokens.Dstas.Models;
using Dxs.Bsv.Tokens.Dstas.Parsing;

namespace Dxs.Bsv.Script.Read;

public class LockingScriptReader : BaseScriptReader
{
    private class DetectContext
    {
        public bool Result { get; set; } = true;
        public bool OpReturnReached { get; set; }
    }

    private readonly Dictionary<ScriptType, DetectContext> _typeDetector =
        new()
        {
            { ScriptType.P2PKH, new DetectContext() },
            { ScriptType.P2MPKH, new DetectContext() },
            { ScriptType.P2STAS, new DetectContext() },
            { ScriptType.Mnee1Sat, new DetectContext() },
            { ScriptType.NullData, new DetectContext() },
        };
    private readonly DstasLockingScriptParser _dstasCandidate = new();

    private LockingScriptReader(
        BitcoinStreamReader bitcoinStreamReader,
        int length,
        Network network
    ) : base(bitcoinStreamReader, length, network) { }

    public ScriptType ScriptType
    {
        get
        {
            if (ScriptTypeOverride.HasValue)
                return ScriptTypeOverride.Value;

            foreach (var (type, value) in _typeDetector)
            {
                if (value.Result)
                    return type;
            }

            return ScriptType.Unknown;
        }
    }

    public Address Address { get; private set; }
    public DstasLockingSemantics Dstas { get; private set; }

    public List<byte[]> Data { get; private set; } //TODO [Oleg] use slices

    private void Read()
    {
        var count = ReadInternal();

        if (ReadBytes != ExpectedLength)
            BitcoinStreamReader.ReadNBytes((ulong)(ExpectedLength - ReadBytes));

        if (count == -1) return;

        foreach (var (type, value) in _typeDetector)
        {
            if (!value.Result) continue;

            var sample = ScriptSamples.ByType[type];
            _typeDetector[type].Result = _typeDetector[type].OpReturnReached || sample.Count == count;
        }

        TryFinalizeDstasFromCandidate();
    }

    protected override bool HandleToken(ScriptReadToken token, int tokenIdx, bool isLastToken)
    {
        _dstasCandidate.ProcessToken(token, tokenIdx);

        var goOn = false;

        foreach (var (type, value) in _typeDetector)
        {
            if (!value.Result) continue;

            var sample = ScriptSamples.ByType[type];

            if (!value.OpReturnReached)
            {
                if (sample.Count == tokenIdx)
                {
                    if (token.OpCode == OpCode.OP_RETURN)
                    {
                        value.OpReturnReached = true;
                        goOn = true;
                    }
                }
                else
                {
                    var newValue = sample.Count > tokenIdx && sample[tokenIdx].Same(token);

                    _typeDetector[type].Result = newValue;

                    if (newValue)
                    {
                        if (sample[tokenIdx].IsReceiverId)
                        {
                            Address = new Address(token.Bytes, ScriptType.P2PKH, Network);
                        }

                        // if (sample[tokenIdx].IsData)
                        // {
                        //     AddData(token.Bytes.ToArray());
                        // }
                    }

                    goOn |= newValue;
                }
            }
            else
            {
                AddData(token.Bytes.ToArray());

                goOn = true;
            }
        }

        goOn |= _dstasCandidate.ShouldContinue;

        return goOn;
    }

    public static LockingScriptReader Read(string hex, Network network)
    {
        var bytes = hex.FromHexString();

        return Read(bytes, network);
    }

    public static LockingScriptReader Read(byte[] bytes, Network network)
    {
        using var stream = new MemoryStream(bytes);

        return Read(stream, network);
    }

    public static LockingScriptReader Read(Stream stream, Network network)
    {
        using var bitcoinStreamReader = new BitcoinStreamReader(stream);

        return Read(bitcoinStreamReader, (int)stream.Length, network);
    }

    public static LockingScriptReader Read(BitcoinStreamReader bitcoinStreamReader, int expectedLength, Network network)
    {
        var reader = new LockingScriptReader(bitcoinStreamReader, expectedLength, network);
        reader.Read();

        return reader;
    }

    private void AddData(byte[] data)
    {
        Data ??= new List<byte[]>();
        Data.Add(data);
    }

    private void TryFinalizeDstasFromCandidate()
    {
        if (!_dstasCandidate.TryBuild(out var dstas))
            return;

        ScriptTypeOverride = ScriptType.DSTAS;
        if (dstas.Owner.Length == 20)
            Address = new Address(dstas.Owner, ScriptType.DSTAS, Network);

        Dstas = dstas;
    }

    private ScriptType? ScriptTypeOverride { get; set; }
}
