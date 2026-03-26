#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dxs.Bsv.Tokens.Validation;

public sealed class StasDependencyPolicy
{
    public StasDependencyFacts Derive(StasLineageTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var withFee = false;
        var allInputsKnown = true;
        string? stasFrom = null;
        string? firstInputHash160 = null;
        string? firstInputTokenId = null;
        bool? firstInputFrozen = null;
        string? firstInputActionType = null;
        int? dstasSpendingType = null;
        var stasInputsCount = 0;
        var inputTokens = new HashSet<string>(StringComparer.Ordinal);
        var illegalRoots = new HashSet<string>(StringComparer.Ordinal);
        var missingDependencies = new HashSet<string>(StringComparer.Ordinal);
        var inputOptionalDataFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var firstStasInputSeen = false;

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
            var isInputStas = StasScriptPolicy.IsStas(inputType);

            if (i == 0)
            {
                firstInputHash160 = inputOutput.Hash160;
            }
            else if (i == transaction.Inputs.Count - 1)
            {
                withFee = StasScriptPolicy.IsRedeemTarget(inputType);
            }

            if (!isInputStas)
                continue;

            stasInputsCount++;

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

        return new StasDependencyFacts(
            withFee,
            allInputsKnown,
            stasFrom,
            firstInputHash160,
            firstInputTokenId,
            firstInputFrozen,
            firstInputActionType,
            dstasSpendingType,
            stasInputsCount,
            inputTokens.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            illegalRoots.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            missingDependencies.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            inputOptionalDataFingerprints);
    }
}
