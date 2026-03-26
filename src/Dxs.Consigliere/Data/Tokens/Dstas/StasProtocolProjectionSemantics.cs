#nullable enable
using System;
using System.Linq;

using Dxs.Bsv.Script;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Data.Tokens.Dstas;

public static class StasProtocolProjectionSemantics
{
    public static string? GetProtocolType(MetaTransaction transaction)
        => !string.IsNullOrWhiteSpace(transaction.StasProtocolType)
            ? transaction.StasProtocolType
            : GetLegacyProtocolType(transaction);

    public static string GetValidationStatus(MetaTransaction transaction)
        => !string.IsNullOrWhiteSpace(transaction.StasValidationStatus)
            ? transaction.StasValidationStatus
            : GetLegacyValidationStatus(transaction);

    public static bool CanProjectTokenOutputs(MetaTransaction transaction)
        => transaction.CanProjectTokenOutputs
            ?? string.Equals(GetValidationStatus(transaction), TokenProjectionValidationStatus.Valid, StringComparison.Ordinal);

    public static bool ShouldProjectOutput(MetaTransaction transaction, MetaOutput output)
    {
        if (output is null || string.IsNullOrWhiteSpace(output.Address))
            return false;

        return output.Type switch
        {
            ScriptType.P2PKH => true,
            ScriptType.P2MPKH => true,
            ScriptType.P2STAS => CanProjectTokenOutputs(transaction),
            ScriptType.DSTAS => CanProjectTokenOutputs(transaction),
            _ => false
        };
    }

    private static string? GetLegacyProtocolType(MetaTransaction transaction)
    {
        if ((transaction.Outputs ?? []).Any(x => x.Type == ScriptType.DSTAS)
            || transaction.DstasSpendingType is not null
            || !string.IsNullOrWhiteSpace(transaction.DstasEventType))
            return TokenProjectionProtocolType.Dstas;

        return transaction.IsStas || (transaction.Outputs ?? []).Any(x => x.Type == ScriptType.P2STAS)
            ? TokenProjectionProtocolType.Stas
            : null;
    }

    private static string GetLegacyValidationStatus(MetaTransaction transaction)
    {
        if (transaction.IsIssue)
            return transaction.IsValidIssue ? TokenProjectionValidationStatus.Valid : TokenProjectionValidationStatus.Invalid;

        if ((transaction.IllegalRoots?.Count ?? 0) > 0)
            return TokenProjectionValidationStatus.Invalid;

        return transaction.AllStasInputsKnown && (transaction.MissingTransactions?.Count ?? 0) == 0
            ? TokenProjectionValidationStatus.Valid
            : TokenProjectionValidationStatus.Unknown;
    }
}
