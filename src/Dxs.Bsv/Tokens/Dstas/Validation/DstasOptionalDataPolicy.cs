#nullable enable
using System;
using System.Linq;
using Dxs.Bsv.Tokens.Dstas.Models;

namespace Dxs.Bsv.Tokens.Dstas.Validation;

public sealed class DstasOptionalDataPolicy
{
    public bool? Derive(DstasLineageFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (!facts.IsStas)
            return null;

        if (facts.InputOptionalDataFingerprints.Count > 0 && facts.OutputOptionalDataFingerprints.Count == 0)
            return false;

        foreach (var fingerprint in facts.OutputOptionalDataFingerprints)
        {
            if (!facts.InputOptionalDataFingerprints.Contains(fingerprint, StringComparer.Ordinal))
                return false;
        }

        return true;
    }
}
