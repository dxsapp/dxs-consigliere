#nullable enable
using System.Collections.Generic;

namespace Dxs.Bsv.Tokens.Dstas.Models;

public sealed record DstasLockingSemantics(
    byte[] Owner,
    byte[]? SecondFieldRaw,
    byte? SecondFieldOpCode,
    byte[] ActionDataRaw,
    string ActionType,
    byte[] Redemption,
    byte[] Flags,
    bool FreezeEnabled,
    bool ConfiscationEnabled,
    bool Frozen,
    IReadOnlyList<byte[]> ServiceFields,
    IReadOnlyList<byte[]> OptionalData,
    byte[]? RequestedScriptHash
);
