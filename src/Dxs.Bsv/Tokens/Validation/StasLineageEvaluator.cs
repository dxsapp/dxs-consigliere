#nullable enable
namespace Dxs.Bsv.Tokens.Validation;

public sealed class StasLineageEvaluator : IStasLineageEvaluator
{
    private readonly StasProtocolLineageEvaluator _inner = new();

    public StasLineageEvaluation Evaluate(StasLineageTransaction transaction)
        => _inner.Evaluate(transaction);
}
