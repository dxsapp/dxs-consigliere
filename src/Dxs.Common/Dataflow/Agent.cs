namespace Dxs.Common.Dataflow;

public static class Agent
{
    public static IAgent<TMessage> Start<TMessage>(Action<TMessage> action, CancellationTokenSource cts = null)
        => new StatelessAgent<TMessage>(action, cts);

    public static IAgent<TMessage> Start<TMessage>(Func<TMessage, Task> action, CancellationTokenSource cts = null)
        => new StatelessAgent<TMessage>(action, cts);


    public static IAgent<TMessage> Start<TState, TMessage>(
        TState initialState,
        Func<TState, TMessage, Task<TState>> action,
        CancellationTokenSource cts = null
    )
        => new StatefulAgent<TState, TMessage>(initialState, action, cts);
}
