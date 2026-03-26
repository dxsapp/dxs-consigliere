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
    {
        if ((transaction.Outputs ?? []).Any(x => x.Type == ScriptType.DSTAS)
            || transaction.DstasSpendingType is not null
            || !string.IsNullOrWhiteSpace(transaction.DstasEventType))
            return TokenProjectionProtocolType.Dstas;

        return transaction.IsStas || (transaction.Outputs ?? []).Any(x => x.Type == ScriptType.P2STAS)
            ? TokenProjectionProtocolType.Stas
            : null;
    }

    public static bool ShouldProjectOutput(MetaTransaction transaction, MetaOutput output)
    {
        if (output is null || string.IsNullOrWhiteSpace(output.Address))
            return false;

        return output.Type switch
        {
            ScriptType.P2PKH => true,
            ScriptType.P2MPKH => true,
            ScriptType.P2STAS => transaction.IsValidIssue || (transaction.AllStasInputsKnown && !transaction.IllegalRoots.Any()),
            ScriptType.DSTAS => transaction.IsValidIssue || (transaction.AllStasInputsKnown && !transaction.IllegalRoots.Any()),
            _ => false
        };
    }
}
