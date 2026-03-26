#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dxs.Bsv.Tokens.Validation;

public sealed class StasOutputPolicy
{
    public StasOutputFacts Derive(StasLineageTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var withNote = transaction.Outputs.Count > 0 && transaction.Outputs[^1].Type == Dxs.Bsv.Script.ScriptType.NullData;
        string? redeemAddress = null;
        bool? firstOutputFrozen = null;
        var outputTokens = new HashSet<string>(StringComparer.Ordinal);
        var outputOptionalDataFingerprints = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < transaction.Outputs.Count; i++)
        {
            var output = transaction.Outputs[i];
            var outputIsStas = StasScriptPolicy.IsStas(output.Type);

            if (outputIsStas)
            {
                if (!string.IsNullOrWhiteSpace(output.TokenId))
                    outputTokens.Add(output.TokenId);

                if (firstOutputFrozen is null && output.DstasFrozen is not null)
                    firstOutputFrozen = output.DstasFrozen == true;

                if (!string.IsNullOrWhiteSpace(output.DstasOptionalDataFingerprint))
                    outputOptionalDataFingerprints.Add(output.DstasOptionalDataFingerprint);
            }

            if (i == 0 && StasScriptPolicy.IsRedeemTarget(output.Type))
                redeemAddress = output.Address;
        }

        return new StasOutputFacts(
            withNote,
            redeemAddress,
            firstOutputFrozen,
            outputTokens.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            outputOptionalDataFingerprints);
    }
}
