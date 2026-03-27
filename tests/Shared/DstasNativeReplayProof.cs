using Dxs.Bsv;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.ScriptEvaluation;

namespace Dxs.Tests.Shared;

public static class DstasNativeReplayProof
{
    private static readonly TransactionEvaluationService Sut = new();

    public static void AssertConformanceVector(string id)
    {
        var vector = DstasConformanceVectorFixture.Load(id);
        var tx = Transaction.Parse(vector.TxHex, Network.Mainnet);
        var prevouts = vector.Prevouts
            .OrderBy(x => x.InputIndex)
            .Select(ToOutPoint)
            .ToArray();

        var result = Sut.EvaluateTransaction(tx, new DictionaryPrevoutResolver(prevouts));

        Assert.Equal(vector.ExpectedSuccess, result.Success);

        if (!vector.ExpectedSuccess && vector.FailedInputs is { Count: > 0 })
        {
            var failedInputs = result.Inputs
                .Where(x => !x.Success)
                .Select(x => x.InputIndex)
                .OrderBy(x => x)
                .ToArray();

            Assert.Equal(vector.FailedInputs.OrderBy(x => x).ToArray(), failedInputs);
        }
    }

    public static void AssertProtocolOwnerChain(string chainId)
    {
        var chain = DstasProtocolOwnerFixture.LoadChain(chainId);

        foreach (var fixture in chain.Transactions)
        {
            var tx = Transaction.Parse(fixture.TxHex, Network.Mainnet);
            var prevouts = DstasProtocolOwnerFixture.ResolvePrevouts(chain, fixture)
                .OrderBy(x => x.InputIndex)
                .Select(ToOutPoint)
                .ToArray();

            Assert.Equal(tx.Inputs.Count, prevouts.Length);

            var result = Sut.EvaluateTransaction(tx, new DictionaryPrevoutResolver(prevouts));

            Assert.True(
                result.Success,
                $"{chain.Id}/{fixture.Label} failed: {string.Join(",", result.Inputs.Where(x => !x.Success).Select(x => $"{x.InputIndex}:{x.ErrorCode}"))}");
        }
    }

    private static OutPoint ToOutPoint(DstasConformancePrevout prevout)
        => new(prevout.TxId, null, string.Empty, (ulong)prevout.Satoshis, (uint)prevout.Vout, prevout.LockingScriptHex, ScriptType.Unknown);

    private static OutPoint ToOutPoint(DstasProtocolPrevoutFixture prevout)
        => new(prevout.TxId, null, string.Empty, (ulong)prevout.Satoshis, (uint)prevout.Vout, prevout.LockingScriptHex, ScriptType.Unknown);
}
