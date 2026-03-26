#nullable enable
using Dxs.Bsv.Tokens.Validation;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Data.Transactions;

public sealed class StasDerivedTransactionStateEvaluator
{
    private readonly IStasLineageEvaluator _lineageEvaluator = new StasProtocolLineageEvaluator();

    public StasDerivedTransactionState Evaluate(
        MetaTransaction transaction,
        IReadOnlyDictionary<string, MetaTransaction> parentsByTxId
    )
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(parentsByTxId);

        var lineage = new StasLineageTransaction(
            transaction.Id,
            (transaction.Inputs ?? [])
            .Select(input => new StasLineageInput(
                input.TxId,
                input.Vout,
                input.DstasSpendingType,
                BuildParentTransaction(input.TxId, input.Vout, parentsByTxId)))
            .ToArray(),
            (transaction.Outputs ?? [])
            .Select(BuildOutput)
            .ToArray());

        var evaluation = _lineageEvaluator.Evaluate(lineage);
        var protocolType = GetProtocolType(transaction, evaluation);
        var validationStatus = GetValidationStatus(evaluation);

        return new StasDerivedTransactionState(
            evaluation.IsStas,
            evaluation.IsIssue,
            evaluation.IsValidIssue,
            evaluation.IsRedeem,
            evaluation.IsWithFee,
            evaluation.IsWithNote,
            evaluation.AllInputsKnown,
            evaluation.RedeemAddress,
            evaluation.StasFrom,
            evaluation.DstasEventType,
            evaluation.DstasSpendingType,
            evaluation.DstasInputFrozen,
            evaluation.DstasOutputFrozen,
            evaluation.DstasOptionalDataContinuity,
            evaluation.TokenIds,
            evaluation.IllegalRoots,
            evaluation.MissingDependencies,
            protocolType,
            validationStatus,
            string.Equals(validationStatus, TokenProjectionValidationStatus.Valid, StringComparison.Ordinal));
    }

    public static Dictionary<string, object> ToPatchValues(StasDerivedTransactionState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new Dictionary<string, object>
        {
            [nameof(MetaTransaction.IsStas)] = state.IsStas,
            [nameof(MetaTransaction.IsIssue)] = state.IsIssue,
            [nameof(MetaTransaction.IsValidIssue)] = state.IsValidIssue,
            [nameof(MetaTransaction.IsRedeem)] = state.IsRedeem,
            [nameof(MetaTransaction.IsWithFee)] = state.IsWithFee,
            [nameof(MetaTransaction.IsWithNote)] = state.IsWithNote,
            [nameof(MetaTransaction.AllStasInputsKnown)] = state.AllStasInputsKnown,
            [nameof(MetaTransaction.RedeemAddress)] = state.RedeemAddress,
            [nameof(MetaTransaction.StasFrom)] = state.StasFrom,
            [nameof(MetaTransaction.DstasEventType)] = state.DstasEventType,
            [nameof(MetaTransaction.DstasSpendingType)] = state.DstasSpendingType,
            [nameof(MetaTransaction.DstasInputFrozen)] = state.DstasInputFrozen,
            [nameof(MetaTransaction.DstasOutputFrozen)] = state.DstasOutputFrozen,
            [nameof(MetaTransaction.DstasOptionalDataContinuity)] = state.DstasOptionalDataContinuity,
            [nameof(MetaTransaction.TokenIds)] = state.TokenIds.ToArray(),
            [nameof(MetaTransaction.IllegalRoots)] = state.IllegalRoots.ToArray(),
            [nameof(MetaTransaction.MissingTransactions)] = state.MissingTransactions.ToArray(),
            [nameof(MetaTransaction.StasProtocolType)] = state.ProtocolType,
            [nameof(MetaTransaction.StasValidationStatus)] = state.ValidationStatus,
            [nameof(MetaTransaction.CanProjectTokenOutputs)] = state.CanProjectTokenOutputs
        };
    }

    private static StasLineageParentTransaction? BuildParentTransaction(
        string txId,
        int requiredVout,
        IReadOnlyDictionary<string, MetaTransaction> parentsByTxId
    )
    {
        if (string.IsNullOrWhiteSpace(txId) || !parentsByTxId.TryGetValue(txId, out var parent) || parent is null)
            return null;

        var outputs = (parent.Outputs ?? [])
            .Select(BuildOutput)
            .ToList();

        while (requiredVout >= outputs.Count)
            outputs.Add(new StasLineageOutput(Dxs.Bsv.Script.ScriptType.Unknown));

        return new StasLineageParentTransaction(
            outputs,
            (parent.MissingTransactions?.Count ?? 0) > 0,
            parent.IsIssue,
            parent.IsValidIssue,
            parent.IllegalRoots ?? []);
    }

    private static StasLineageOutput BuildOutput(MetaTransaction.Output output)
        => new(
            output.Type,
            output.Address,
            output.TokenId,
            output.Hash160,
            output.DstasFrozen,
            output.DstasActionType,
            output.DstasOptionalDataFingerprint
        );

    private static string? GetProtocolType(MetaTransaction transaction, StasLineageEvaluation evaluation)
    {
        if (!evaluation.IsStas)
            return null;

        return (transaction.Outputs ?? []).Any(x => x.Type == Dxs.Bsv.Script.ScriptType.DSTAS)
            || evaluation.DstasSpendingType is not null
            || !string.IsNullOrWhiteSpace(evaluation.DstasEventType)
            ? TokenProjectionProtocolType.Dstas
            : TokenProjectionProtocolType.Stas;
    }

    private static string? GetValidationStatus(StasLineageEvaluation evaluation)
    {
        if (!evaluation.IsStas)
            return null;

        if (evaluation.IsIssue)
            return evaluation.IsValidIssue ? TokenProjectionValidationStatus.Valid : TokenProjectionValidationStatus.Invalid;

        if (evaluation.IllegalRoots.Count > 0)
            return TokenProjectionValidationStatus.Invalid;

        return evaluation.AllInputsKnown && evaluation.MissingDependencies.Count == 0
            ? TokenProjectionValidationStatus.Valid
            : TokenProjectionValidationStatus.Unknown;
    }
}
