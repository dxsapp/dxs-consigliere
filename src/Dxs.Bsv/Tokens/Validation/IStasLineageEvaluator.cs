#nullable enable
namespace Dxs.Bsv.Tokens.Validation;

public interface IStasLineageEvaluator
{
    StasLineageEvaluation Evaluate(StasLineageTransaction transaction);
}
