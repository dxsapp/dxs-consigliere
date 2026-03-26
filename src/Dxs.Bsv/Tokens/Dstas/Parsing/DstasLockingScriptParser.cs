#nullable enable
using System.Collections.Generic;
using System.Linq;

using Dxs.Bsv.Protocol;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;
using Dxs.Bsv.Tokens.Dstas.Models;

namespace Dxs.Bsv.Tokens.Dstas.Parsing;

public sealed class DstasLockingScriptParser
{
    public static DstasLockingSemantics? Parse(LockingScriptReader reader)
    {
        if (reader is null)
            return null;

        return reader.Dstas is null
            ? null
            : new DstasLockingSemantics(
                reader.Dstas.Owner.ToArray(),
                reader.Dstas.SecondFieldRaw?.ToArray(),
                reader.Dstas.SecondFieldOpCode,
                reader.Dstas.ActionDataRaw.ToArray(),
                reader.Dstas.ActionType,
                reader.Dstas.Redemption.ToArray(),
                reader.Dstas.Flags.ToArray(),
                reader.Dstas.FreezeEnabled,
                reader.Dstas.ConfiscationEnabled,
                reader.Dstas.Frozen,
                reader.Dstas.ServiceFields.Select(x => x.ToArray()).ToArray(),
                reader.Dstas.OptionalData.Select(x => x.ToArray()).ToArray(),
                reader.Dstas.RequestedScriptHash?.ToArray());
    }

    private bool _headerInvalid;
    private bool _probeActive;

    private byte[]? _owner;
    private bool _secondCaptured;
    private bool _secondIsPushData;
    private byte[]? _secondFieldRaw;
    private byte? _secondFieldOpCode;

    private int _opReturnIdx = -1;
    private bool _postValid;
    private byte[]? _redemption;
    private byte[]? _flags;
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

        if (_redemption is null)
        {
            if (!IsPushDataToken(token) || token.Bytes.Length != 20)
            {
                _postValid = false;
                return;
            }

            _redemption = token.Bytes.ToArray();
            return;
        }

        if (_flags is null)
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

    public bool TryBuild(out DstasLockingSemantics? semantics)
    {
        semantics = null;

        if (_headerInvalid || !_probeActive || !_secondCaptured || _owner is null)
            return false;

        if (_opReturnIdx < 16 || !_postValid || _redemption is null || _flags is null)
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
            actionDataRaw = _secondFieldRaw ?? [];

            if (actionDataRaw.Length > 1 && actionDataRaw[0] == 0x02 && IsActionTypeMarker(actionDataRaw[1]))
            {
                frozen = true;
                actionDataRaw = actionDataRaw[1..];
            }
        }

        var actionType = DstasActionTypes.Empty;
        byte[]? requestedScriptHash = null;
        if (actionDataRaw.Length > 0)
        {
            actionType = actionDataRaw[0] switch
            {
                0x01 => DstasActionTypes.Swap,
                0x02 => DstasActionTypes.Confiscation,
                0x03 => DstasActionTypes.Freeze,
                _ => DstasActionTypes.Unknown
            };

            if (actionType == DstasActionTypes.Swap && actionDataRaw.Length >= 33)
                requestedScriptHash = actionDataRaw[1..33];
        }

        semantics = new DstasLockingSemantics(
            _owner,
            _secondFieldRaw,
            _secondFieldOpCode,
            actionDataRaw,
            actionType,
            _redemption,
            _flags,
            _freezeEnabled,
            _confiscationEnabled,
            frozen,
            _serviceFields.ToArray(),
            _optionalData.ToArray(),
            requestedScriptHash);

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

    private static bool IsPushDataOpCode(byte opCodeNum)
        => opCodeNum < (byte)OpCode.OP_PUSHDATA1
           || opCodeNum == (byte)OpCode.OP_PUSHDATA1
           || opCodeNum == (byte)OpCode.OP_PUSHDATA2
           || opCodeNum == (byte)OpCode.OP_PUSHDATA4;

    private static bool IsActionTypeMarker(byte marker)
        => marker is 0x01 or 0x02 or 0x03;
}
