using NBitcoin;

namespace Dxs.Bsv.ScriptEvaluation;

public sealed class BsvScriptExecutionPolicy
{
    public static BsvScriptExecutionPolicy RepoDefault { get; } = new();

    public ScriptVerify ScriptVerify { get; init; } = ScriptVerify.Mandatory | ScriptVerify.ForkId;
    public bool AllowOpReturn { get; init; } = true;
}
