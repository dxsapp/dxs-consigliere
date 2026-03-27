using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.ScriptEvaluation;
using Dxs.Tests.Shared;

namespace Dxs.Bsv.Tests.Conformance;

public class NativeInterpreterParityTests
{
    private readonly TransactionEvaluationService _sut = new();

    [Fact]
    public void NativeInterpreter_MatchesVendoredDstasConformanceVectors()
    {
        var vectors = DstasConformanceVectorFixture.LoadAll();

        foreach (var vector in vectors)
        {
            var tx = Transaction.Parse(vector.TxHex, Network.Mainnet);
            var prevouts = vector.Prevouts
                .OrderBy(x => x.InputIndex)
                .Select(ToOutPoint)
                .ToArray();

            var result = _sut.EvaluateTransaction(tx, new DictionaryPrevoutResolver(prevouts));

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
    }

    [Fact]
    public void NativeInterpreter_AcceptsVendoredProtocolOwnerChains()
    {
        var chains = DstasProtocolOwnerFixture.LoadAll();

        foreach (var chain in chains)
        {
            foreach (var fixture in chain.Transactions)
            {
                var tx = Transaction.Parse(fixture.TxHex, Network.Mainnet);
                if (fixture.Prevouts.Count != tx.Inputs.Count)
                    continue;

                var prevouts = fixture.Prevouts
                    .OrderBy(x => x.InputIndex)
                    .Select(ToOutPoint)
                    .ToArray();

                var result = _sut.EvaluateTransaction(tx, new DictionaryPrevoutResolver(prevouts));

                Assert.True(result.Success, $"{chain.Id}/{fixture.Label} failed: {string.Join(",", result.Inputs.Where(x => !x.Success).Select(x => $"{x.InputIndex}:{x.ErrorCode}"))}");
            }
        }
    }

    private static OutPoint ToOutPoint(DstasConformancePrevout prevout)
        => new(prevout.TxId, null, string.Empty, (ulong)prevout.Satoshis, (uint)prevout.Vout, prevout.LockingScriptHex, ScriptType.Unknown);

    private static OutPoint ToOutPoint(DstasProtocolPrevoutFixture prevout)
        => new(prevout.TxId, null, string.Empty, (ulong)prevout.Satoshis, (uint)prevout.Vout, prevout.LockingScriptHex, ScriptType.Unknown);
}
