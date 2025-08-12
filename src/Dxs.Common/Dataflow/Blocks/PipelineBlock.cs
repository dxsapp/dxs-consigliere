using System.Threading.Tasks.Dataflow;
using Dxs.Common.Extensions;

namespace Dxs.Common.Dataflow.Blocks;

public class PipelineBlock<TInput, TOutput>: PipelineBlock<TInput>, IPropagatorBlock<TInput, TOutput>
{
    private readonly ISourceBlock<TOutput> _sourceBlock;

    private PipelineBlock(ITargetBlock<TInput> targetBlock, ISourceBlock<TOutput> sourceBlock): base(targetBlock, sourceBlock)
    {
        _sourceBlock = sourceBlock;
    }

    public PipelineBlock(IPropagatorBlock<TInput, TOutput> block): this(block, block) {}

    public PipelineBlock<TInput, TOutputNew> Add<TOutputNew>(IPropagatorBlock<TOutput, TOutputNew> block)
    {
        _sourceBlock.LinkToWithCompletion(block);

        return new PipelineBlock<TInput, TOutputNew>(DataflowBlock.Encapsulate(this, block));
    }

    public PipelineBlock<TInput> Add(ITargetBlock<TOutput> block)
    {
        _sourceBlock.LinkToWithCompletion(block);

        return new PipelineBlock<TInput>(this, block);
    }

    #region Implementation of ISourceBlock<out TOutput>

    TOutput ISourceBlock<TOutput>.ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target, out bool messageConsumed) =>
        _sourceBlock.ConsumeMessage(messageHeader, target, out messageConsumed);

    IDisposable ISourceBlock<TOutput>.LinkTo(ITargetBlock<TOutput> target, DataflowLinkOptions linkOptions) =>
        _sourceBlock.LinkTo(target, linkOptions);

    void ISourceBlock<TOutput>.ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target) =>
        _sourceBlock.ReleaseReservation(messageHeader, target);

    bool ISourceBlock<TOutput>.ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target) =>
        _sourceBlock.ReserveMessage(messageHeader, target);

    #endregion
}

public class PipelineBlock<TInput>(ITargetBlock<TInput> targetBlock, IDataflowBlock lastBlock) : ITargetBlock<TInput>
{
    public Task Complete()
    {
        targetBlock.Complete();
        return lastBlock.Completion;
    }

    #region Implementation of IDataflowBlock

    void IDataflowBlock.Complete() => targetBlock.Complete();

    void IDataflowBlock.Fault(Exception exception) => targetBlock.Fault(exception);

    Task IDataflowBlock.Completion => lastBlock.Completion;

    #endregion

    #region Implementation of ITargetBlock<in TInput>

    DataflowMessageStatus ITargetBlock<TInput>.OfferMessage(
        DataflowMessageHeader messageHeader,
        TInput messageValue,
        ISourceBlock<TInput> source,
        bool consumeToAccept
    ) => targetBlock.OfferMessage(messageHeader, messageValue, source, consumeToAccept);

    #endregion

}