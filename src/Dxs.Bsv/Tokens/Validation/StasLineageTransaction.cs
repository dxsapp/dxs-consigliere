#nullable enable
using System.Collections.Generic;

namespace Dxs.Bsv.Tokens.Validation;

public sealed record StasLineageTransaction(
    string TxId,
    IReadOnlyList<StasLineageInput> Inputs,
    IReadOnlyList<StasLineageOutput> Outputs
);
