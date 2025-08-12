using System.Threading.Tasks.Dataflow;

namespace Dxs.Common.Dataflow;

public sealed class StatelessAgent<TMessage>: IAgent<TMessage>
{
    private readonly ActionBlock<TMessage> _actionBlock;

    public StatelessAgent(Action<TMessage> action, CancellationTokenSource cts = null)
    {
        var options = new ExecutionDataflowBlockOptions
        {
            CancellationToken = cts?.Token ?? CancellationToken.None
        };
        _actionBlock = new ActionBlock<TMessage>(action, options);
    }

    public StatelessAgent(Func<TMessage, Task> action, CancellationTokenSource cts = null)
    {
        var options = new ExecutionDataflowBlockOptions
        {
            CancellationToken = cts?.Token ?? CancellationToken.None,
        };
        _actionBlock = new ActionBlock<TMessage>(action, options);
    }

    public int MessagesInQueue => _actionBlock.InputCount;
    public void Complete() => _actionBlock.Complete();

    public Task Send(TMessage message) => _actionBlock.SendAsync(message);

    public void Post(TMessage message) => _actionBlock.Post(message);
}