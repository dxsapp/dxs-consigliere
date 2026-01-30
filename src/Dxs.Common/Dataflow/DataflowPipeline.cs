using System.Threading.Tasks.Dataflow;

using Dxs.Common.Dataflow.Blocks;

namespace Dxs.Common.Dataflow;

public static class DataflowPipeline
{
    public static PipelineBlock<TIn, TOut> Create<TIn, TOut>(IPropagatorBlock<TIn, TOut> block) => new(block);
}
