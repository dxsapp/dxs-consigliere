#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using Dxs.Bsv.Extensions;
using Dxs.Bsv.Models;
using Dxs.Bsv.ScriptEvaluation.NBitcoinFork;
using NBitcoin;

namespace Dxs.Bsv.ScriptEvaluation;

public sealed class TransactionEvaluationService
{
    public TransactionEvaluationResult EvaluateTransaction(
        Models.Transaction transaction,
        IPrevoutResolver prevoutResolver,
        BsvScriptExecutionPolicy? policy = null,
        bool enableTrace = false,
        int traceLimit = 2048)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(prevoutResolver);

        policy ??= BsvScriptExecutionPolicy.RepoDefault;
        var nativeTransaction = NBitcoin.Transaction.Parse(transaction.Hex, transaction.Network.ToNBitcoin());
        var results = new List<ScriptEvaluationResult>(transaction.Inputs.Count);

        for (var i = 0; i < transaction.Inputs.Count; i++)
        {
            var input = transaction.Inputs[i];
            if (!prevoutResolver.TryResolve(input.TxId, input.Vout, out var prevout))
            {
                results.Add(new ScriptEvaluationResult(i, false, "PrevoutMissing", $"Missing prevout {input.TxId}:{input.Vout}."));
                continue;
            }

            var spentOutput = new TxOut(Money.Satoshis((long)prevout.Satoshis), NBitcoin.Script.FromBytesUnsafe(prevout.ScriptPubKey));
            var checker = new BsvTransactionChecker(nativeTransaction, i, spentOutput);
            var context = new BsvScriptEvaluationContext
            {
                ScriptVerify = policy.ScriptVerify,
                AllowOpReturn = policy.AllowOpReturn,
                TraceEnabled = enableTrace,
                TraceLimit = traceLimit
            };

            var success = context.VerifyScript(nativeTransaction.Inputs[i].ScriptSig, spentOutput.ScriptPubKey, checker);
            results.Add(new ScriptEvaluationResult(
                i,
                success,
                context.Error.ToString(),
                context.ThrownException?.Message,
                enableTrace ? context.Trace.ToArray() : null));
        }

        return new TransactionEvaluationResult(results.All(x => x.Success), results);
    }
}
