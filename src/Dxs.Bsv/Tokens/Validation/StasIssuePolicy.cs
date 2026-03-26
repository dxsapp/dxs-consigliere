#nullable enable
using System;
using System.Linq;

namespace Dxs.Bsv.Tokens.Validation;

public sealed class StasIssuePolicy
{
    public StasIssueSemantics Derive(StasOutputFacts outputFacts, StasDependencyFacts dependencyFacts)
    {
        ArgumentNullException.ThrowIfNull(outputFacts);
        ArgumentNullException.ThrowIfNull(dependencyFacts);

        var hasStasOutputs = outputFacts.OutputTokens.Count > 0;
        var isStas = hasStasOutputs || dependencyFacts.StasInputsCount > 0;
        var isIssue = isStas && hasStasOutputs && dependencyFacts.StasInputsCount == 0;
        var isValidIssue =
            isIssue &&
            dependencyFacts.AllInputsKnown &&
            outputFacts.OutputTokens.Count == 1 &&
            outputFacts.OutputTokens[0] == dependencyFacts.FirstInputHash160;

        var tokenIds = outputFacts.OutputTokens
            .Concat(dependencyFacts.InputTokens)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return new StasIssueSemantics(isStas, isIssue, isValidIssue, tokenIds);
    }
}
