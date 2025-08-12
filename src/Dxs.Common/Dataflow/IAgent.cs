namespace Dxs.Common.Dataflow;

public interface IAgent<in TMessage>
{
    Task Send(TMessage message);
    void Post(TMessage message);
    int MessagesInQueue { get; }
    void Complete();
}