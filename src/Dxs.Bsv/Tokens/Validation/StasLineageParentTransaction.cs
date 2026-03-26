#nullable enable
using System;
using System.Collections.Generic;

namespace Dxs.Bsv.Tokens.Validation;

public sealed record StasLineageParentTransaction(
    IReadOnlyList<StasLineageOutput> Outputs,
    bool HasMissingDependencies = false,
    bool IsIssue = false,
    bool IsValidIssue = false,
    IReadOnlyList<string>? IllegalRoots = null
)
{
    public StasLineageOutput GetOutput(int vout)
    {
        if (vout < 0 || vout >= Outputs.Count)
            throw new ArgumentOutOfRangeException(nameof(vout), $"Parent transaction output `{vout}` is out of range.");

        return Outputs[vout];
    }
}
