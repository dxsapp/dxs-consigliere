using System.Threading.Tasks.Dataflow;
using TrustMargin.Common.Extensions;

namespace Dxs.Common.Dataflow;

public sealed class StatefulAgent<TState, TMessage>: IAgent<TMessage>, IDisposable
{
    private TState _state;
    private readonly ActionBlock<TMessage> _actionBlock;

    public StatefulAgent(
        TState initialState,
        Func<TState, TMessage, Task<TState>> action,
        CancellationTokenSource cts = null)
    {
        var options = new ExecutionDataflowBlockOptions
        {
            CancellationToken = cts?.Token ?? CancellationToken.None,
            MaxDegreeOfParallelism = 1
        };

        _actionBlock = new ActionBlock<TMessage>(
            async msg => _state = await action(_state, msg),
            options
        );
        _state = initialState;
    }

    public int MessagesInQueue => _actionBlock.InputCount;

    public Task Send(TMessage message) => _actionBlock.SendAsync(message);

    public void Post(TMessage message) => _actionBlock.Post(message);

    public void Complete() => _actionBlock.Complete();

    public void Dispose()
    {
        _state.TryDispose();
    }
}