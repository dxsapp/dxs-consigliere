using System.Threading.Tasks.Dataflow;

namespace Dxs.Common.Dataflow.Blocks;

/// <summary>
/// Enumerates queued <see cref="IAsyncEnumerable{T}"/> to linked blocks.
/// </summary>
public class EnumeratorBlock<T> : IPropagatorBlock<IAsyncEnumerable<T>, T>
{
    private readonly ActionBlock<IAsyncEnumerable<T>> _enumerator;
    private readonly BufferBlock<T> _buffer;

    private ITargetBlock<IAsyncEnumerable<T>> EnumeratorAsTarget => _enumerator;
    private IPropagatorBlock<T, T> BufferAsPropagator => _buffer;

    public int InputCount => _enumerator.InputCount;

    public int OutputCount => _buffer.Count;

    public EnumeratorBlock(ExecutionDataflowBlockOptions options = null)
    {
        _enumerator = new ActionBlock<IAsyncEnumerable<T>>(Enumerate, options ?? new ExecutionDataflowBlockOptions());
        _buffer = new BufferBlock<T>(new DataflowBlockOptions { BoundedCapacity = 1 });

        _enumerator.Completion.ContinueWith(t =>
        {
            if (t.Exception is { } exception)
                BufferAsPropagator.Fault(exception);
            else
                BufferAsPropagator.Complete();
        });
    }

    private async Task Enumerate(IAsyncEnumerable<T> source)
    {
        await foreach (var elem in source)
            await _buffer.SendAsync(elem);
    }

    #region Implementation of IDataflowBlock

    public void Complete() => EnumeratorAsTarget.Complete();

    public void Fault(Exception exception) => EnumeratorAsTarget.Fault(exception);

    public Task Completion => BufferAsPropagator.Completion;

    #endregion

    #region Implementation of ISourceBlock<out T>

    public T ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target, out bool messageConsumed) =>
        BufferAsPropagator.ConsumeMessage(messageHeader, target, out messageConsumed);

    public IDisposable LinkTo(ITargetBlock<T> target, DataflowLinkOptions linkOptions) =>
        BufferAsPropagator.LinkTo(target, linkOptions);

    public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<T> target) =>
        BufferAsPropagator.ReleaseReservation(messageHeader, target);

    public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target) =>
        BufferAsPropagator.ReserveMessage(messageHeader, target);

    #endregion

    #region Implementation of ITargetBlock<in T>

    public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, IAsyncEnumerable<T> messageValue, ISourceBlock<IAsyncEnumerable<T>> source, bool consumeToAccept) =>
        EnumeratorAsTarget.OfferMessage(messageHeader, messageValue, source, consumeToAccept);

    #endregion

}
