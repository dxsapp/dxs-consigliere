using System;
using System.Collections.Generic;
using System.IO;

using Dxs.Bsv.Protocol;

namespace Dxs.Bsv.Script.Read;

public class LockingScriptReader : BaseScriptReader
{
    public sealed class DstasInfo
    {
        public byte[] Owner { get; init; }
        public byte[] SecondFieldRaw { get; init; }
        public byte? SecondFieldOpCode { get; init; }
        public byte[] ActionDataRaw { get; init; }
        public string ActionType { get; init; }
        public byte[] Redemption { get; init; }
        public byte[] Flags { get; init; }
        public bool FreezeEnabled { get; init; }
        public bool ConfiscationEnabled { get; init; }
        public bool Frozen { get; init; }
        public IReadOnlyList<byte[]> ServiceFields { get; init; }
        public IReadOnlyList<byte[]> OptionalData { get; init; }
        public byte[] RequestedScriptHash { get; init; }
    }

    private class DetectContext
    {
        public bool Result { get; set; } = true;
        public bool OpReturnReached { get; set; }
    }

    private sealed class DstasCandidateParser
    {
        private bool _headerInvalid;
        private bool _probeActive;

        private byte[] _owner;
        private bool _secondCaptured;
        private bool _secondIsPushData;
        private byte[] _secondFieldRaw;
        private byte? _secondFieldOpCode;

        private int _opReturnIdx = -1;
        private bool _postValid;
        private byte[] _redemption;
        private byte[] _flags;
        private bool _freezeEnabled;
        private bool _confiscationEnabled;
        private int _expectedServiceFieldsCount;
        private readonly List<byte[]> _serviceFields = [];
        private readonly List<byte[]> _optionalData = [];

        public bool ShouldContinue => _probeActive;

        public void ProcessToken(ScriptReadToken token, int tokenIdx)
        {
            if (_headerInvalid)
                return;

            if (tokenIdx == 0)
            {
                if (!IsPushDataToken(token) || token.Bytes.Length == 0)
                {
                    _headerInvalid = true;
                    _probeActive = false;
                    return;
                }

                _owner = token.Bytes.ToArray();
                _probeActive = true;
                return;
            }

            if (!_probeActive)
                return;

            if (tokenIdx == 1)
            {
                if (IsPushDataToken(token))
                {
                    _secondCaptured = true;
                    _secondIsPushData = true;
                    _secondFieldRaw = token.Bytes.ToArray();
                    _secondFieldOpCode = null;
                    return;
                }

                if (token.OpCodeNum is (byte)OpCode.OP_0 or (byte)OpCode.OP_2)
                {
                    _secondCaptured = true;
                    _secondIsPushData = false;
                    _secondFieldRaw = null;
                    _secondFieldOpCode = token.OpCodeNum;
                    return;
                }

                _headerInvalid = true;
                _probeActive = false;
                return;
            }

            if (!_secondCaptured)
            {
                _headerInvalid = true;
                _probeActive = false;
                return;
            }

            if (token.OpCodeNum == (byte)OpCode.OP_RETURN)
            {
                StartPostParse(tokenIdx);
                return;
            }

            if (_opReturnIdx < 0 || !_postValid)
                return;

            if (_redemption == null)
            {
                if (!IsPushDataToken(token) || token.Bytes.Length != 20)
                {
                    _postValid = false;
                    return;
                }

                _redemption = token.Bytes.ToArray();
                return;
            }

            if (_flags == null)
            {
                if (IsPushDataToken(token))
                {
                    _flags = token.Bytes.ToArray();
                }
                else if (token.OpCodeNum == (byte)OpCode.OP_0)
                {
                    _flags = [];
                }
                else
                {
                    _postValid = false;
                    return;
                }

                var rightMostFlagsByte = _flags.Length > 0 ? _flags[^1] : (byte)0;
                _freezeEnabled = (rightMostFlagsByte & 0x01) == 0x01;
                _confiscationEnabled = (rightMostFlagsByte & 0x02) == 0x02;
                _expectedServiceFieldsCount = (_freezeEnabled ? 1 : 0) + (_confiscationEnabled ? 1 : 0);
                return;
            }

            if (!IsPushDataToken(token))
            {
                _postValid = false;
                return;
            }

            var data = token.Bytes.ToArray();
            if (_serviceFields.Count < _expectedServiceFieldsCount)
                _serviceFields.Add(data);
            else
                _optionalData.Add(data);
        }

        public bool TryBuild(out DstasInfo dstasInfo)
        {
            dstasInfo = null;

            if (_headerInvalid || !_probeActive || !_secondCaptured)
                return false;

            if (_opReturnIdx < 16 || !_postValid || _redemption == null || _flags == null)
                return false;

            if (_serviceFields.Count < _expectedServiceFieldsCount)
                return false;

            var frozen = false;
            byte[] actionDataRaw;
            if (!_secondIsPushData)
            {
                frozen = _secondFieldOpCode == (byte)OpCode.OP_2;
                actionDataRaw = [];
            }
            else
            {
                actionDataRaw = _secondFieldRaw;

                if (actionDataRaw.Length > 1 &&
                    actionDataRaw[0] == 0x02 &&
                    IsActionTypeMarker(actionDataRaw[1]))
                {
                    frozen = true;
                    actionDataRaw = actionDataRaw[1..];
                }
            }

            var actionType = "empty";
            byte[] requestedScriptHash = null;
            if (actionDataRaw.Length > 0)
            {
                actionType = actionDataRaw[0] switch
                {
                    0x01 => "swap",
                    0x02 => "confiscation",
                    0x03 => "freeze",
                    _ => "unknown"
                };

                // Swap payload starts with: action(1) + requestedScriptHash(32) + requestedPkh(20) + ...
                if (actionType == "swap" && actionDataRaw.Length >= 33)
                    requestedScriptHash = actionDataRaw[1..33];
            }

            dstasInfo = new DstasInfo
            {
                Owner = _owner,
                SecondFieldRaw = _secondFieldRaw,
                SecondFieldOpCode = _secondFieldOpCode,
                ActionDataRaw = actionDataRaw,
                ActionType = actionType,
                Redemption = _redemption,
                Flags = _flags,
                FreezeEnabled = _freezeEnabled,
                ConfiscationEnabled = _confiscationEnabled,
                Frozen = frozen,
                ServiceFields = _serviceFields,
                OptionalData = _optionalData,
                RequestedScriptHash = requestedScriptHash,
            };

            return true;
        }

        private void StartPostParse(int tokenIdx)
        {
            _opReturnIdx = tokenIdx;
            _postValid = true;
            _redemption = null;
            _flags = null;
            _freezeEnabled = false;
            _confiscationEnabled = false;
            _expectedServiceFieldsCount = 0;
            _serviceFields.Clear();
            _optionalData.Clear();
        }

        private static bool IsPushDataToken(ScriptReadToken token)
            => token.OpCodeNum > 0 && IsPushDataOpCode(token.OpCodeNum);
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
    private readonly DstasCandidateParser _dstasCandidate = new();

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
    public DstasInfo Dstas { get; private set; }

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

    private static bool IsPushDataOpCode(byte opCodeNum)
        => opCodeNum < (byte)OpCode.OP_PUSHDATA1
           || opCodeNum == (byte)OpCode.OP_PUSHDATA1
           || opCodeNum == (byte)OpCode.OP_PUSHDATA2
           || opCodeNum == (byte)OpCode.OP_PUSHDATA4;

    private static bool IsActionTypeMarker(byte marker)
        => marker is 0x01 or 0x02 or 0x03;

    private ScriptType? ScriptTypeOverride { get; set; }
}
