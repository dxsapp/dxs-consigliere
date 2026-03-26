#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using Dxs.Bsv.Script;
using Dxs.Bsv.Tokens.Dstas.Models;
using Dxs.Bsv.Tokens.Dstas.Validation;

namespace Dxs.Bsv.Tokens.Validation;

public sealed class StasProtocolLineageEvaluator : IStasLineageEvaluator
{
    private readonly DstasSemanticDeriver _dstasSemanticDeriver = new();

    public StasLineageEvaluation Evaluate(StasLineageTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var withNote = transaction.Outputs.Count > 0 && transaction.Outputs[^1].Type == ScriptType.NullData;
        var withFee = false;
        var allInputsKnown = true;
        string? stasFrom = null;
        string? firstInputHash160 = null;
        string? firstInputTokenId = null;
        var inputTokens = new HashSet<string>(StringComparer.Ordinal);
        var illegalRoots = new HashSet<string>(StringComparer.Ordinal);
        var missingDependencies = new HashSet<string>(StringComparer.Ordinal);
        string? redeemAddress = null;
        var outputTokens = new HashSet<string>(StringComparer.Ordinal);
        bool? firstInputFrozen = null;
        bool? firstOutputFrozen = null;
        string? firstInputActionType = null;
        int? dstasSpendingType = null;
        var inputOptionalDataFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var outputOptionalDataFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var firstStasInputSeen = false;

        for (var i = 0; i < transaction.Outputs.Count; i++)
        {
            var output = transaction.Outputs[i];
            var outputIsStas = IsStas(output.Type);

            if (outputIsStas)
            {
                if (!string.IsNullOrWhiteSpace(output.TokenId))
                    outputTokens.Add(output.TokenId);

                if (firstOutputFrozen is null && output.DstasFrozen is not null)
                    firstOutputFrozen = output.DstasFrozen == true;

                if (!string.IsNullOrWhiteSpace(output.DstasOptionalDataFingerprint))
                    outputOptionalDataFingerprints.Add(output.DstasOptionalDataFingerprint);
            }

            if (i == 0 && IsRedeemTarget(output.Type))
                redeemAddress = output.Address;
        }

        for (var i = 0; i < transaction.Inputs.Count; i++)
        {
            var input = transaction.Inputs[i];
            var parent = input.Parent;

            if (parent is null)
            {
                allInputsKnown = false;
                missingDependencies.Add(input.TxId);
                continue;
            }

            var inputOutput = parent.GetOutput(input.Vout);
            var inputType = inputOutput.Type;
            var isInputStas = IsStas(inputType);

            if (i == 0)
            {
                firstInputHash160 = inputOutput.Hash160;
            }
            else if (i == transaction.Inputs.Count - 1)
            {
                withFee = IsRedeemTarget(inputType);
            }

            if (!isInputStas)
                continue;

            if (!firstStasInputSeen)
            {
                firstStasInputSeen = true;
                stasFrom = inputOutput.Address;
                firstInputTokenId = inputOutput.TokenId;

                if (inputOutput.DstasFrozen is not null)
                    firstInputFrozen = inputOutput.DstasFrozen == true;

                if (!string.IsNullOrWhiteSpace(inputOutput.DstasActionType))
                    firstInputActionType = inputOutput.DstasActionType;
            }

            if (dstasSpendingType is null && input.DstasSpendingType is not null)
                dstasSpendingType = input.DstasSpendingType;

            if (!string.IsNullOrWhiteSpace(inputOutput.DstasOptionalDataFingerprint))
                inputOptionalDataFingerprints.Add(inputOutput.DstasOptionalDataFingerprint);

            if (parent.HasMissingDependencies)
                missingDependencies.Add(input.TxId);

            if (!string.IsNullOrWhiteSpace(inputOutput.TokenId))
                inputTokens.Add(inputOutput.TokenId);

            if (parent.IsIssue)
            {
                if (!parent.IsValidIssue)
                    illegalRoots.Add(input.TxId);
            }
            else if (parent.IllegalRoots is not null)
            {
                foreach (var illegalRoot in parent.IllegalRoots.Where(x => !string.IsNullOrWhiteSpace(x)))
                    illegalRoots.Add(illegalRoot);
            }
        }

        var hasStasOutputs = outputTokens.Count > 0;
        var stasInputsCount = transaction.Inputs.Count(x => x.Parent is not null && IsStas(x.Parent.GetOutput(x.Vout).Type));
        var isStas = hasStasOutputs || stasInputsCount > 0;
        var isIssue = isStas && hasStasOutputs && stasInputsCount == 0;
        var isValidIssue =
            isIssue &&
            allInputsKnown &&
            outputTokens.Count == 1 &&
            outputTokens.Single() == firstInputHash160;

        var firstOutput = transaction.Outputs.Count > 0 ? transaction.Outputs[0] : null;
        var dstasDerived = _dstasSemanticDeriver.Derive(new DstasLineageFacts(
            isStas,
            allInputsKnown,
            stasInputsCount,
            firstOutput is not null && IsRedeemTarget(firstOutput.Type),
            redeemAddress,
            stasFrom,
            firstInputTokenId,
            firstOutput?.Hash160,
            firstInputFrozen,
            firstOutputFrozen,
            firstInputActionType,
            dstasSpendingType,
            inputOptionalDataFingerprints,
            outputOptionalDataFingerprints));

        return new StasLineageEvaluation(
            isStas,
            isIssue,
            isValidIssue,
            dstasDerived.IsRedeem,
            isStas && withFee,
            isStas && withNote,
            isStas && allInputsKnown,
            dstasDerived.RedeemAddress,
            isStas ? stasFrom : null,
            dstasDerived.EventType,
            dstasDerived.SpendingType,
            dstasDerived.InputFrozen,
            dstasDerived.OutputFrozen,
            dstasDerived.OptionalDataContinuity,
            outputTokens.Concat(inputTokens).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            illegalRoots.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            missingDependencies.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static bool IsStas(ScriptType type) => type is ScriptType.P2STAS or ScriptType.DSTAS;

    private static bool IsRedeemTarget(ScriptType type) => type is ScriptType.P2PKH or ScriptType.P2MPKH;
}
