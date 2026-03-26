#nullable enable
namespace Dxs.Bsv.Tokens.Validation;

public sealed record StasLineageInput(
    string TxId,
    int Vout,
    int? DstasSpendingType = null,
    StasLineageParentTransaction? Parent = null
);
