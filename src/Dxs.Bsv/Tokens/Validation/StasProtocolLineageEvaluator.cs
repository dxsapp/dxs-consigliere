#nullable enable
using System;
using Dxs.Bsv.Tokens.Dstas.Models;
using Dxs.Bsv.Tokens.Dstas.Validation;

namespace Dxs.Bsv.Tokens.Validation;

public sealed class StasProtocolLineageEvaluator : IStasLineageEvaluator
{
    private readonly StasOutputPolicy _outputPolicy = new();
    private readonly StasDependencyPolicy _dependencyPolicy = new();
    private readonly StasIssuePolicy _issuePolicy = new();
    private readonly DstasSemanticDeriver _dstasSemanticDeriver = new();

    public StasLineageEvaluation Evaluate(StasLineageTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var outputFacts = _outputPolicy.Derive(transaction);
        var dependencyFacts = _dependencyPolicy.Derive(transaction);
        var issueSemantics = _issuePolicy.Derive(outputFacts, dependencyFacts);

        var firstOutput = transaction.Outputs.Count > 0 ? transaction.Outputs[0] : null;
        var dstasDerived = _dstasSemanticDeriver.Derive(new DstasLineageFacts(
            issueSemantics.IsStas,
            dependencyFacts.AllInputsKnown,
            dependencyFacts.StasInputsCount,
            firstOutput is not null && StasScriptPolicy.IsRedeemTarget(firstOutput.Type),
            outputFacts.RedeemAddress,
            dependencyFacts.StasFrom,
            dependencyFacts.FirstInputTokenId,
            firstOutput?.Hash160,
            dependencyFacts.FirstInputFrozen,
            outputFacts.FirstOutputFrozen,
            dependencyFacts.FirstInputActionType,
            dependencyFacts.DstasSpendingType,
            dependencyFacts.InputOptionalDataFingerprints,
            outputFacts.OutputOptionalDataFingerprints));

        return new StasLineageEvaluation(
            issueSemantics.IsStas,
            issueSemantics.IsIssue,
            issueSemantics.IsValidIssue,
            dstasDerived.IsRedeem,
            issueSemantics.IsStas && dependencyFacts.WithFee,
            issueSemantics.IsStas && outputFacts.WithNote,
            issueSemantics.IsStas && dependencyFacts.AllInputsKnown,
            dstasDerived.RedeemAddress,
            issueSemantics.IsStas ? dependencyFacts.StasFrom : null,
            dstasDerived.EventType,
            dstasDerived.SpendingType,
            dstasDerived.InputFrozen,
            dstasDerived.OutputFrozen,
            dstasDerived.OptionalDataContinuity,
            issueSemantics.TokenIds,
            dependencyFacts.IllegalRoots,
            dependencyFacts.MissingDependencies);
    }
}
