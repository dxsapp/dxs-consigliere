#nullable enable
using System.Linq;

using Dxs.Bsv;
using Dxs.Bsv.Tokens.Dstas.Models;

namespace Dxs.Consigliere.Data.Transactions.Dstas;

internal sealed record DstasMetaOutputMapping(
    string? Flags,
    bool? FreezeEnabled,
    bool? ConfiscationEnabled,
    bool? Frozen,
    string? FreezeAuthority,
    string? ConfiscationAuthority,
    string[]? ServiceFields,
    string? ActionType,
    string? ActionData,
    string? RequestedScriptHash,
    string[]? OptionalData,
    string? OptionalDataFingerprint)
{
    public static DstasMetaOutputMapping FromSemantics(DstasLockingSemantics? semantics)
    {
        if (semantics is null)
        {
            return new DstasMetaOutputMapping(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        var serviceFields = semantics.ServiceFields.Select(x => x.ToHexString()).ToArray();
        var optionalData = semantics.OptionalData.Select(x => x.ToHexString()).ToArray();
        var actionData = semantics.ActionDataRaw.Length > 0
            ? semantics.ActionDataRaw.ToHexString()
            : null;

        return new DstasMetaOutputMapping(
            semantics.Flags.ToHexString(),
            semantics.FreezeEnabled,
            semantics.ConfiscationEnabled,
            semantics.Frozen,
            serviceFields.Length > 0 && semantics.FreezeEnabled ? serviceFields[0] : null,
            semantics.ConfiscationEnabled
                ? serviceFields[semantics.FreezeEnabled ? 1 : 0]
                : null,
            serviceFields,
            semantics.ActionType,
            actionData,
            semantics.RequestedScriptHash?.ToHexString(),
            optionalData,
            optionalData.Length > 0 ? string.Join("|", optionalData) : null);
    }
}
