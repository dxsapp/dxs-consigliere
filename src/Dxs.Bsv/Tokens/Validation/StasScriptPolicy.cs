#nullable enable
using Dxs.Bsv.Script;

namespace Dxs.Bsv.Tokens.Validation;

internal static class StasScriptPolicy
{
    public static bool IsStas(ScriptType type) => type is ScriptType.P2STAS or ScriptType.DSTAS;

    public static bool IsRedeemTarget(ScriptType type) => type is ScriptType.P2PKH or ScriptType.P2MPKH;
}
