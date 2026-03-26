#nullable enable
using System.Collections.Generic;

namespace Dxs.Consigliere.Data.Transactions;

public sealed record StasDerivedTransactionState(
    bool IsStas,
    bool IsIssue,
    bool IsValidIssue,
    bool IsRedeem,
    bool IsWithFee,
    bool IsWithNote,
    bool AllStasInputsKnown,
    string? RedeemAddress,
    string? StasFrom,
    string? DstasEventType,
    int? DstasSpendingType,
    bool? DstasInputFrozen,
    bool? DstasOutputFrozen,
    bool? DstasOptionalDataContinuity,
    IReadOnlyList<string> TokenIds,
    IReadOnlyList<string> IllegalRoots,
    IReadOnlyList<string> MissingTransactions,
    string? ProtocolType,
    string? ValidationStatus,
    bool CanProjectTokenOutputs
)
{
    public bool AllInputsKnown => AllStasInputsKnown;
}
